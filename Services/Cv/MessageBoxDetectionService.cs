using MoneyPenny.Services.Ocr;
using MoneyPenny.Services.Rag.Ingestion;
using OpenCvSharp;

namespace MoneyPenny.Services.Cv;

public sealed class MessageBoxDetectionService : IMessageBoxDetectionService
{
    private readonly ITesseractOcrService _ocrService;
    private readonly IImageTextExtractionService _imageTextExtractionService;
    private readonly ILogger<MessageBoxDetectionService> _logger;

    public MessageBoxDetectionService(
        ITesseractOcrService ocrService,
        IImageTextExtractionService imageTextExtractionService,
        ILogger<MessageBoxDetectionService> logger)
    {
        _ocrService = ocrService;
        _imageTextExtractionService = imageTextExtractionService;
        _logger = logger;
    }

    public async Task<MessageBoxDetectionResult> DetectAsync(
        byte[] imageBytes,
        MessageBoxTextEngine textEngine = MessageBoxTextEngine.Tesseract,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
        {
            return MessageBoxDetectionResult.Fail("La imagen está vacía.");
        }

        try
        {
            using var source = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (source.Empty())
            {
                return MessageBoxDetectionResult.Fail("No se pudo decodificar la imagen.");
            }

            var scale = ComputeWorkingScale(source.Width, source.Height);
            using var working = new Mat();
            Cv2.Resize(source, working, new Size(), scale, scale, InterpolationFlags.Area);

            var icons = DetectRedErrorIcons(working);
            if (icons.Count == 0)
            {
                return MessageBoxDetectionResult.NotFound(
                    "No se detectó un icono rojo de error típico de MessageBox de Windows.");
            }

            var bestIcon = icons[0];
            var dialog = FindDialogAroundIcon(working, bestIcon);
            MessageBoxElement? titleBar = null;
            MessageBoxElement? primaryButton = null;

            if (dialog is not null)
            {
                titleBar = DetectTitleBar(working, dialog);
                primaryButton = DetectPrimaryButton(working, dialog);
            }

            var elements = new List<MessageBoxElement> { ScaleElement(bestIcon, scale) };
            if (dialog is not null)
            {
                elements.Add(ScaleElement(dialog, scale));
            }

            if (titleBar is not null)
            {
                elements.Add(ScaleElement(titleBar, scale));
            }

            if (primaryButton is not null)
            {
                elements.Add(ScaleElement(primaryButton, scale));
            }

            var scaledElements = elements.ToArray();
            var confidence = ComputeConfidence(scaledElements);
            var detected = confidence >= 0.45;

            string? titleText = null;
            string? messageText = null;

            if (detected)
            {
                var titleBarElement = scaledElements.FirstOrDefault(item => item.Type == "TitleBar");
                if (titleBarElement is not null)
                {
                    var titleRect = ClampRect(
                        new Rect(titleBarElement.X, titleBarElement.Y, titleBarElement.Width, titleBarElement.Height),
                        source.Width,
                        source.Height);
                    titleText = await ExtractTextFromRegionAsync(
                        source,
                        titleRect,
                        textEngine,
                        OpenAiImageTextExtractionService.MessageBoxTitleExtractionPrompt,
                        cancellationToken);
                }

                var messageRect = ComputeMessageTextRegion(scaledElements, source.Width, source.Height);
                if (messageRect.HasValue)
                {
                    elements.Add(new MessageBoxElement
                    {
                        Type = "MessageText",
                        X = messageRect.Value.X,
                        Y = messageRect.Value.Y,
                        Width = messageRect.Value.Width,
                        Height = messageRect.Value.Height,
                        Score = 0.8
                    });

                    messageText = await ExtractTextFromRegionAsync(
                        source,
                        messageRect.Value,
                        textEngine,
                        OpenAiImageTextExtractionService.MessageBoxMessageExtractionPrompt,
                        cancellationToken);
                }
            }

            var summary = BuildSummary(detected, confidence, elements, titleText, messageText, textEngine);

            return new MessageBoxDetectionResult
            {
                Success = true,
                Detected = detected,
                Confidence = confidence,
                Summary = summary,
                Elements = elements,
                TitleText = titleText,
                MessageText = messageText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al analizar MessageBox con OpenCV.");
            return MessageBoxDetectionResult.Fail("No se pudo analizar la imagen con OpenCV.");
        }
    }

    private static double ComputeWorkingScale(int width, int height)
    {
        var longest = Math.Max(width, height);
        if (longest <= 1600)
        {
            return 1d;
        }

        return 1600d / longest;
    }

    private static MessageBoxElement ScaleElement(MessageBoxElement element, double scale)
    {
        if (Math.Abs(scale - 1d) < 0.001)
        {
            return element;
        }

        var inverse = 1d / scale;
        return new MessageBoxElement
        {
            Type = element.Type,
            X = (int)Math.Round(element.X * inverse),
            Y = (int)Math.Round(element.Y * inverse),
            Width = (int)Math.Round(element.Width * inverse),
            Height = (int)Math.Round(element.Height * inverse),
            Score = element.Score
        };
    }

    private static List<MessageBoxElement> DetectRedErrorIcons(Mat image)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

        using var lowerRed1 = new Mat();
        using var upperRed1 = new Mat();
        using var lowerRed2 = new Mat();
        using var upperRed2 = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 70, 70), new Scalar(12, 255, 255), lowerRed1);
        Cv2.InRange(hsv, new Scalar(168, 70, 70), new Scalar(180, 255, 255), lowerRed2);
        using var mask = new Mat();
        Cv2.BitwiseOr(lowerRed1, lowerRed2, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var imageArea = image.Width * image.Height;
        var candidates = new List<MessageBoxElement>();

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 120 || area > imageArea * 0.02)
            {
                continue;
            }

            var rect = Cv2.BoundingRect(contour);
            var aspect = rect.Width / (double)Math.Max(1, rect.Height);
            if (aspect < 0.65 || aspect > 1.45)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            if (perimeter <= 0)
            {
                continue;
            }

            var circularity = 4 * Math.PI * area / (perimeter * perimeter);
            if (circularity < 0.45)
            {
                continue;
            }

            candidates.Add(new MessageBoxElement
            {
                Type = "ErrorIcon",
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Score = Math.Min(1d, circularity + 0.15)
            });
        }

        return candidates
            .OrderByDescending(item => item.Score * item.Width * item.Height)
            .ToList();
    }

    private static MessageBoxElement? FindDialogAroundIcon(Mat image, MessageBoxElement icon)
    {
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(3, 3), 0);

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 40, 120);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var iconCenter = new Point(icon.X + icon.Width / 2, icon.Y + icon.Height / 2);
        MessageBoxElement? best = null;
        var bestScore = 0d;

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < icon.Width * 3 || rect.Height < icon.Height * 2)
            {
                continue;
            }

            if (rect.Width > image.Width * 0.85 || rect.Height > image.Height * 0.85)
            {
                continue;
            }

            if (!rect.Contains(iconCenter))
            {
                continue;
            }

            var aspect = rect.Width / (double)Math.Max(1, rect.Height);
            if (aspect < 1.1 || aspect > 4.5)
            {
                continue;
            }

            using var roi = new Mat(gray, rect);
            var mean = Cv2.Mean(roi).Val0;
            if (mean > 225 || mean < 170)
            {
                continue;
            }

            var score = 1d - Math.Abs(aspect - 2.2) / 2.2;
            score += rect.Contains(iconCenter) ? 0.35 : 0;
            score += mean is >= 190 and <= 215 ? 0.25 : 0;

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = new MessageBoxElement
            {
                Type = "Dialog",
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Score = Math.Min(1d, score)
            };
        }

        if (best is not null)
        {
            return best;
        }

        var fallbackWidth = Math.Min(image.Width - icon.X + 20, Math.Max(icon.Width * 8, 260));
        var fallbackHeight = Math.Max(icon.Height * 3, 120);
        var fallbackX = Math.Max(0, icon.X - 20);
        var fallbackY = Math.Max(0, icon.Y - 30);
        fallbackWidth = Math.Min(fallbackWidth, image.Width - fallbackX);
        fallbackHeight = Math.Min(fallbackHeight, image.Height - fallbackY);

        return new MessageBoxElement
        {
            Type = "Dialog",
            X = fallbackX,
            Y = fallbackY,
            Width = fallbackWidth,
            Height = fallbackHeight,
            Score = 0.35
        };
    }

    private static MessageBoxElement? DetectTitleBar(Mat image, MessageBoxElement dialog)
    {
        var titleHeight = Math.Clamp((int)Math.Round(dialog.Height * 0.18), 18, 40);
        var titleRect = new Rect(dialog.X, dialog.Y, dialog.Width, titleHeight);
        titleRect = ClampRect(titleRect, image.Width, image.Height);

        using var gray = new Mat();
        Cv2.CvtColor(new Mat(image, titleRect), gray, ColorConversionCodes.BGR2GRAY);
        var mean = Cv2.Mean(gray).Val0;
        if (mean > 210)
        {
            return null;
        }

        return new MessageBoxElement
        {
            Type = "TitleBar",
            X = titleRect.X,
            Y = titleRect.Y,
            Width = titleRect.Width,
            Height = titleRect.Height,
            Score = Math.Min(1d, (210 - mean) / 60d + 0.4)
        };
    }

    private static MessageBoxElement? DetectPrimaryButton(Mat image, MessageBoxElement dialog)
    {
        var searchTop = dialog.Y + (int)Math.Round(dialog.Height * 0.62);
        var searchHeight = dialog.Y + dialog.Height - searchTop - 8;
        if (searchHeight < 20)
        {
            return null;
        }

        var searchRect = new Rect(dialog.X + 8, searchTop, dialog.Width - 16, searchHeight);
        searchRect = ClampRect(searchRect, image.Width, image.Height);

        using var roiColor = new Mat(image, searchRect);
        using var gray = new Mat();
        Cv2.CvtColor(roiColor, gray, ColorConversionCodes.BGR2GRAY);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 30, 90);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        MessageBoxElement? best = null;
        var bestScore = 0d;
        var centerX = dialog.X + dialog.Width / 2;

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < 40 || rect.Height < 16 || rect.Height > 40)
            {
                continue;
            }

            var aspect = rect.Width / (double)Math.Max(1, rect.Height);
            if (aspect < 1.4 || aspect > 6.5)
            {
                continue;
            }

            using var buttonGray = new Mat(gray, rect);
            var mean = Cv2.Mean(buttonGray).Val0;
            if (mean < 170 || mean > 240)
            {
                continue;
            }

            var absoluteCenter = searchRect.X + rect.X + rect.Width / 2;
            var centeredness = 1d - Math.Min(1d, Math.Abs(absoluteCenter - centerX) / (dialog.Width * 0.35));
            var score = centeredness * 0.6 + (aspect is >= 2 and <= 4 ? 0.25 : 0.1);

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = new MessageBoxElement
            {
                Type = "PrimaryButton",
                X = searchRect.X + rect.X,
                Y = searchRect.Y + rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Score = Math.Min(1d, score)
            };
        }

        return best;
    }

    private static Rect ClampRect(Rect rect, int maxWidth, int maxHeight)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, maxWidth - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, maxHeight - 1));
        var width = Math.Clamp(rect.Width, 1, maxWidth - x);
        var height = Math.Clamp(rect.Height, 1, maxHeight - y);
        return new Rect(x, y, width, height);
    }

    private async Task<string?> ExtractTextFromRegionAsync(
        Mat source,
        Rect region,
        MessageBoxTextEngine textEngine,
        string visionPrompt,
        CancellationToken cancellationToken)
    {
        if (region.Width < 12 || region.Height < 10)
        {
            return null;
        }

        try
        {
            using var crop = new Mat(source, region);

            if (textEngine == MessageBoxTextEngine.Vision)
            {
                var cropBytes = EncodeCropForVision(crop);
                var text = await _imageTextExtractionService.ExtractFromBytesAsync(
                    cropBytes,
                    visionPrompt,
                    cancellationToken);
                return CleanOcrText(text);
            }

            var preparedBytes = PreprocessCropForOcr(crop);
            var ocrText = await _ocrService.ExtractTextAsync(preparedBytes, cancellationToken);
            return CleanOcrText(ocrText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo extraer texto de la región MessageBox.");
            return null;
        }
    }

    private static byte[] EncodeCropForVision(Mat crop)
    {
        using var working = crop.Clone();
        if (working.Width < 480)
        {
            var scale = 480d / working.Width;
            var targetHeight = Math.Max(1, (int)Math.Round(working.Height * scale));
            using var resized = new Mat();
            Cv2.Resize(working, resized, new Size(480, targetHeight), 0, 0, InterpolationFlags.Cubic);
            Cv2.ImEncode(".png", resized, out var resizedBytes);
            return resizedBytes;
        }

        Cv2.ImEncode(".png", working, out var bytes);
        return bytes;
    }

    private static Rect? ComputeMessageTextRegion(
        IReadOnlyList<MessageBoxElement> elements,
        int imageWidth,
        int imageHeight)
    {
        var dialog = elements.FirstOrDefault(item => item.Type == "Dialog");
        var icon = elements.FirstOrDefault(item => item.Type == "ErrorIcon");
        if (dialog is null || icon is null)
        {
            return null;
        }

        var titleBar = elements.FirstOrDefault(item => item.Type == "TitleBar");
        var button = elements.FirstOrDefault(item => item.Type == "PrimaryButton");

        var top = titleBar is not null
            ? titleBar.Y + titleBar.Height + 6
            : dialog.Y + Math.Clamp((int)Math.Round(dialog.Height * 0.18), 18, 42);

        top = Math.Max(top, icon.Y - 2);

        var bottom = button is not null
            ? button.Y - 8
            : dialog.Y + dialog.Height - 14;

        var left = Math.Max(dialog.X + 8, icon.X + icon.Width + 10);
        var right = dialog.X + dialog.Width - 10;
        var width = right - left;
        var height = bottom - top;

        if (width < 24 || height < 16 || bottom <= top)
        {
            left = dialog.X + Math.Clamp((int)Math.Round(dialog.Width * 0.12), 8, 40);
            width = dialog.Width - (left - dialog.X) - 12;
            height = bottom - top;
        }

        if (width < 24 || height < 16)
        {
            return null;
        }

        return ClampRect(new Rect(left, top, width, height), imageWidth, imageHeight);
    }

    private static byte[] PreprocessCropForOcr(Mat crop)
    {
        using var gray = new Mat();
        Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);

        var targetWidth = Math.Max(gray.Width, 480);
        if (gray.Width < targetWidth)
        {
            var scale = targetWidth / (double)gray.Width;
            var targetHeight = Math.Max(1, (int)Math.Round(gray.Height * scale));
            using var resized = new Mat();
            Cv2.Resize(gray, resized, new Size(targetWidth, targetHeight), 0, 0, InterpolationFlags.Cubic);
            using var processed = EnhanceForOcr(resized);
            Cv2.ImEncode(".png", processed, out var bytes);
            return bytes;
        }

        using (var processed = EnhanceForOcr(gray))
        {
            Cv2.ImEncode(".png", processed, out var bytes);
            return bytes;
        }
    }

    private static Mat EnhanceForOcr(Mat gray)
    {
        var output = new Mat();
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
        Cv2.AdaptiveThreshold(
            blurred,
            output,
            255,
            AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.Binary,
            31,
            7);
        return output;
    }

    private static string? CleanOcrText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 1 || char.IsLetterOrDigit(line[0]))
            .ToArray();

        var cleaned = string.Join(Environment.NewLine, lines).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static double ComputeConfidence(IReadOnlyList<MessageBoxElement> elements)
    {
        var score = 0d;
        if (elements.Any(item => item.Type == "ErrorIcon"))
        {
            score += 0.45;
        }

        if (elements.Any(item => item.Type == "Dialog"))
        {
            score += 0.25;
        }

        if (elements.Any(item => item.Type == "TitleBar"))
        {
            score += 0.15;
        }

        if (elements.Any(item => item.Type == "PrimaryButton"))
        {
            score += 0.15;
        }

        return Math.Min(1d, score);
    }

    private static string BuildSummary(
        bool detected,
        double confidence,
        IReadOnlyList<MessageBoxElement> elements,
        string? titleText,
        string? messageText,
        MessageBoxTextEngine textEngine)
    {
        if (!detected)
        {
            return "OpenCV encontró un posible icono de error, pero no una MessageBox completa con confianza suficiente.";
        }

        var engineLabel = textEngine == MessageBoxTextEngine.Vision ? "OpenCV + Vision" : "OpenCV + Tesseract";
        var lines = new List<string>
        {
            $"MessageBox de Windows detectada (confianza {confidence:P0})."
        };

        if (!string.IsNullOrWhiteSpace(titleText))
        {
            lines.Add($"Título ({engineLabel}): {titleText}");
        }

        if (!string.IsNullOrWhiteSpace(messageText))
        {
            lines.Add(string.Empty);
            lines.Add($"Texto del mensaje ({engineLabel}):");
            lines.Add(messageText);
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add($"No se pudo leer el texto del mensaje con {engineLabel} en la región detectada.");
            if (textEngine == MessageBoxTextEngine.Tesseract)
            {
                lines.Add("Prueba «MessageBox (OpenCV + Vision)» para capturas complejas.");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Regiones detectadas:");

        foreach (var element in elements.Where(item => item.Type != "MessageText"))
        {
            var label = element.Type switch
            {
                "ErrorIcon" => "Icono de error",
                "Dialog" => "Ventana de diálogo",
                "TitleBar" => "Barra de título",
                "PrimaryButton" => "Botón principal",
                _ => element.Type
            };

            lines.Add($"- {label}: {element.Width}x{element.Height}px en ({element.X}, {element.Y})");
        }

        var messageRegion = elements.FirstOrDefault(item => item.Type == "MessageText");
        if (messageRegion is not null)
        {
            lines.Add(
                $"- Zona de mensaje OCR: {messageRegion.Width}x{messageRegion.Height}px en ({messageRegion.X}, {messageRegion.Y})");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
