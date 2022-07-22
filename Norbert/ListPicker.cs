
public class ListPicker
{
    public string templateType { get; } = "ListPicker";
    public string version { get; } = "1.0";
    public Data? data { get; set; }
}

public class Data
{
    public Replymessage? replyMessage { get; set; }
    public Content? content { get; set; }
}

public class Replymessage
{
    public string? title { get; set; }
    public string? subtitle { get; set; }
    public string? imageType { get; set; }
    public string? imageData { get; set; }
    public string? imageDescription { get; set; }
}

public class Content
{
    public string? title { get; set; }
    public string? subtitle { get; set; }
    public string? imageType { get; set; }
    public string? imageData { get; set; }
    public string? imageDescription { get; set; }
    public Element[]? elements { get; set; }
}

public class Element
{
    public string? title { get; set; }
    public string? subtitle { get; set; }
    public string? imageType { get; set; }
    public string? imageData { get; set; }
    public string? imageDescription { get; set; }
}
