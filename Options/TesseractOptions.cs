namespace MoneyPenny.Options;

public class TesseractOptions
{
    public const string SectionName = "Tesseract";

    public string TessDataPath { get; set; } = "tessdata";

    public string Languages { get; set; } = "spa+eng";
}
