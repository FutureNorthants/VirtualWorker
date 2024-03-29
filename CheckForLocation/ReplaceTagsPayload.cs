﻿
public class ReplaceTagsPayload
{
    public string message { get; set; }
    public string sovereign { get; set; }
    public Tag[] tags { get; set; }
}

public class Tag
{
    public string tag { get; set; }
    public string NBC { get; set; } = "";
    public string SNC { get; set; } = "";
    public string DDC { get; set; } = "";
    public string CBC { get; set; } = "";
    public string KBC { get; set; } = "";
    public string BCW { get; set; } = "";
    public string ENC { get; set; } = "";

}
