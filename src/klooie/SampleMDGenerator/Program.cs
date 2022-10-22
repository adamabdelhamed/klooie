using PowerArgs;
using System.Reflection;
using System.Text;
using klooie.Samples;

//#Running and recording Gifs for all samples
SampleGifGenerator.RunAll();

Console.WriteLine($"Finding samples...");
var gitRoot = FindGitRoot();
var samples = Sample.FindAll(gitRoot);
$"Samples found: {samples.Count}".ToGreen().WriteLine();
Console.WriteLine($"Finding templates...");
var templates = ReadMeTemplate.FindAll(gitRoot);
$"Templates found: {templates.Count}".ToGreen().WriteLine();
foreach (var template in templates)
{
    Console.Write($"generating readme for template {template.TemplateFilePath}...");
    template.GenerateReadMeFile(samples);
    " done".ToGreen().WriteLine();
}

string FindGitRoot()
{
    var dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
    while(Directory.GetDirectories(dir).Where(d => d.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).None())
    {
        dir = Path.GetDirectoryName(dir);
    }
    return dir;
}

public class SampleParameters
{
    [ArgRequired]
    public string Id { get; set; }
}

class Sample
{
    public string SourceFile { get; private init; }
    public string GifPath => SourceFile.Replace(".cs", ".gif");
    public bool HasGif => File.Exists(GifPath);
    public string GifMD => HasGif ? $"![sample image]({CreateGifUrl()})" : string.Empty;



    public string Code { get; private init; }
    public SampleParameters Parameters { get; private init; }

    public Sample(string SourceFile, string code, SampleParameters parameters)
    {
        this.SourceFile = SourceFile;
        this.Code = code;
        this.Parameters = parameters;
    }

    public static Dictionary<string,Sample> FindAll(string gitRoot)
    {
        var ret = new Dictionary<string, Sample>();
        var sampleStart = "//#Sample";
        var sampleEnd = "//#EndSample";
        var sampleStartLength = sampleStart.Length;
        foreach (var cSharpFile in Directory.GetFiles(gitRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(cSharpFile);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("//#Sample") == false) continue;
                var cmd = line.Substring(sampleStartLength);
                var args = Args.Convert(cmd);
                var parameters = Args.Parse<SampleParameters>(args);

                var sampleCode = "";
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var sampleCodeLine = lines[j];
                    if (sampleCodeLine.StartsWith(sampleEnd))
                    {
                        i = j+1;
                        break;
                    }
                    sampleCode += sampleCodeLine + "\n";
                }

                ret.Add(parameters.Id, new Sample(cSharpFile, sampleCode, parameters));
            }
        }
        return ret;
    }

    private string CreateGifUrl()
    {
        var fileName = GifPath.Replace('\\', '/');
        var startIndex = fileName.LastIndexOf("Samples/");
        var relative = fileName.Substring(startIndex);
        var ret = $"https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/{relative}?raw=true";
        return ret;
    }
}

class ReadMeTemplate
{
    public string TemplateFilePath { get; private init; }
    public string OutputFilePath => Path.Combine(Path.GetDirectoryName(TemplateFilePath), "Readme.md");
    public string[] TemplateMarkdown { get; private init; }

    public ReadMeTemplate(string file)
    {
        TemplateFilePath = file;
        TemplateMarkdown = File.ReadAllLines(file);
    }

    public static List<ReadMeTemplate> FindAll(string gitRoot)
    {
        var ret = new List<ReadMeTemplate>();

        foreach (var templateFile in Directory.GetFiles(gitRoot, "ReadmeTemplate.md", SearchOption.AllDirectories))
        {
            ret.Add(new ReadMeTemplate(templateFile));
        }
        return ret;
    }

    public void GenerateReadMeFile(Dictionary<string, Sample> samples)
    {
        var builder = new StringBuilder();
        foreach(var line in TemplateMarkdown)
        {
            if (line.StartsWith("//#") == false)
            {
                builder.Append(line+"\n");
                continue;
            }
            builder.Append("```cs\n");
            var sampleId = line.Substring("//#".Length);
            var sample = samples[sampleId];
            builder.Append(sample.Code+"\n");
            builder.Append("```\n");
            if(sample.HasGif)
            {
                builder.Append("The sample above creates an application that looks like this.\n\n");
                builder.Append(sample.GifMD + "\n");
            }
        }

        var output = builder.ToString();
        File.WriteAllText(OutputFilePath, output, Encoding.UTF8);
    }
}