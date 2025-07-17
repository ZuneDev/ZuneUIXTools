namespace UIXC;

public enum SourceLanguage
{
    Xml, Asm
}

public static class SourceLanguageEx
{
    public static string GetExtension(this SourceLanguage sourceLanguage)
    {
        return sourceLanguage switch
        {
            SourceLanguage.Xml => ".uix",
            SourceLanguage.Asm => ".uixa",

            _ => throw new ArgumentException("Invalid source language", nameof(sourceLanguage)),
        };
    }
}
