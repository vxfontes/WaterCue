using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace WaterCueWindows.Services;

public record ValidationResult(bool Valid, string Reason);

public enum GroqErrorKind
{
    NoApiKey, Unauthorized, RateLimited, ServerError, NetworkError, Timeout, Unparseable, ReplayedPhoto
}

public class GroqException(GroqErrorKind error, string message) : Exception(message)
{
    public GroqErrorKind Error { get; } = error;
}

public class GroqVisionService
{
    public static readonly GroqVisionService Shared = new();

    private static readonly Uri BaseUrl = new("https://api.groq.com/openai/v1/chat/completions");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private const string SystemPrompt =
        "Você é um validador de hidratação. Responda APENAS JSON válido no formato " +
        "{\"valid\":bool,\"reason\":string}. Não escreva nada antes ou depois do JSON.";

    private const string UserPrompt = """
        Esta é uma captura de webcam ao vivo de um sistema de hidratação. Avalie DUAS condições obrigatórias:

        CONDIÇÃO 1 — ROSTO HUMANO: pelo menos um rosto humano real está parcialmente visível e identificável. Critério mínimo: olhos, nariz ou boca detectáveis com clareza. NÃO conta: mãos isoladas, corpo sem rosto, silhuetas, reflexos, bonecos, fotos de pessoas, avatares.

        CONDIÇÃO 2 — RECIPIENTE EM USO: um recipiente de bebida (copo, garrafa, caneca, squeeze, xícara, copo descartável, garrafa esportiva) está visível E a pessoa está interagindo — segurando, levando à boca, ou claramente prestes a beber. O recipiente pode estar vazio. O recipiente deve estar em primeiro plano ou visivelmente associado à pessoa, não apenas ao fundo.

        valid=true SOMENTE SE as duas condições forem satisfeitas simultaneamente.

        valid=false se qualquer um dos seguintes for verdadeiro:
        - Nenhum rosto humano identificável na imagem
        - Nenhum recipiente de bebida visível ou associado à pessoa
        - A imagem exibe uma tela (monitor, celular, TV mostrando outra imagem ou vídeo)
        - A imagem é foto impressa, foto de foto, ou imagem claramente não capturada ao vivo agora
        - A imagem está muito escura, borrada ou obstruída para avaliar qualquer condição
        - O recipiente contém comida sólida (prato, tigela de comida) e não bebida
        - Há dúvida razoável sobre qualquer condição — na dúvida, prefira false

        reason: escreva 1 frase curta em português com tom bem-humorado e leve. Use criatividade baseada no caso específico detectado. Exemplos de tom (não copie, crie variações):
        - Rosto presente mas sem recipiente → "Seu rosto é lindo, mas cadê a água?"
        - Recipiente presente mas sem rosto → "Ótimo copo! Mas preciso ver você também, não só ele."
        - Imagem escura/borrada → "Tá com medo da câmera? Aparece aí!"
        - Foto de tela detectada → "Tentou me enganar com printscreen? Não rola não."
        - Foto de foto → "Foto antiga não vale, precisa ser ao vivo!"
        - valid=true, pessoa bebendo → algo animado celebrando a hidratação
        - valid=true, pessoa segurando → algo incentivando a beber logo
        """;

    private GroqVisionService() { }

    public async Task<ValidationResult> ValidateAsync(byte[] jpegData, string model)
    {
        var apiKey = SettingsService.Shared.LoadApiKey();
        if (string.IsNullOrEmpty(apiKey))
            throw new GroqException(GroqErrorKind.NoApiKey, "Configure sua Groq API key em Preferências.");

        var resized = ResizeJpeg(jpegData, 1024);
        var b64 = Convert.ToBase64String(resized);

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } },
                        new { type = "text", text = UserPrompt }
                    }
                }
            },
            temperature = 0.1,
            max_tokens = 150,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            throw new GroqException(GroqErrorKind.Timeout, "Groq demorou muito. Tente novamente.");
        }
        catch (Exception ex)
        {
            throw new GroqException(GroqErrorKind.NetworkError, $"Sem conexão. Verifique o Wi-Fi. ({ex.Message})");
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.OK => await ParseResponseAsync(response),
            System.Net.HttpStatusCode.Unauthorized =>
                throw new GroqException(GroqErrorKind.Unauthorized, "API key inválida. Verifique em Preferências."),
            System.Net.HttpStatusCode.TooManyRequests =>
                throw new GroqException(GroqErrorKind.RateLimited, "Servidor ocupado. Tente novamente em instantes."),
            var code =>
                throw new GroqException(GroqErrorKind.ServerError, $"Erro no servidor Groq ({(int)code}). Tente novamente.")
        };
    }

    private static async Task<ValidationResult> ParseResponseAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            using var inner = JsonDocument.Parse(content);
            var valid = inner.RootElement.GetProperty("valid").GetBoolean();
            var reason = inner.RootElement.GetProperty("reason").GetString() ?? "";
            return new ValidationResult(valid, reason);
        }
        catch
        {
            throw new GroqException(GroqErrorKind.Unparseable, "Resposta inesperada. Tente outra foto.");
        }
    }

    private static byte[] ResizeJpeg(byte[] jpegData, int maxDim)
    {
        using var ms = new MemoryStream(jpegData);
        var frame = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var w = frame.PixelWidth;
        var h = frame.PixelHeight;
        var scale = Math.Min((double)maxDim / w, (double)maxDim / h);
        if (scale >= 1.0) return jpegData;

        var scaled = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
        var encoder = new JpegBitmapEncoder { QualityLevel = 70 };
        encoder.Frames.Add(BitmapFrame.Create(scaled));

        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
