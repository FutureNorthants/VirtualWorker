
public class QNAResponse
{
    public Answer[] answers { get; set; }
    public bool activeLearningEnabled { get; set; }
}

public class Answer
{
    public string[] questions { get; set; }
    public string answer { get; set; }
    public float score { get; set; }
    public int id { get; set; }
    public string source { get; set; }
    public bool isDocumentText { get; set; }
    public Metadata[] metadata { get; set; }
    public Context context { get; set; }
}

public class Context
{
    public bool isContextOnly { get; set; }
    public object[] prompts { get; set; }
}

public class Metadata
{
    public string name { get; set; }
    public string value { get; set; }
}
