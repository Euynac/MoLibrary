using System.Text.Json;
using System.Text.RegularExpressions;
using Monica.Tool.Extensions;

namespace Monica.Tool.Text;
/// <summary>
/// Programming language detector
/// </summary>
public class LangDetector
{
    public class LangOption
    {
        public string pattern { get; set; } = string.Empty;
        public int points { get; set; }
        public bool nearTop { get; set; }
    }

    public class Root
    {
        public Dictionary<string, List<LangOption>> Languages { get; set; } = [];
    }

    public enum SupportedLanguages
    {
        Javascript,
        CSharp,
        Blazor,
        Cpp,
        Java,
        Python,
        PHP,
        Ruby,
        C,
        JSON,
        HTML,
        CSS,
        GO,
        Markdown,
        YAML,
        SQL,
        Vue,
        XML,
        Bat,
        Unknown
    }

    public class ScoresResult
    {
        public SupportedLanguages Languages { get; set; }
        public int Scores { get; set; }
    }

    private static readonly IReadOnlyList<LanguageRuleSet> LANGUAGE_RULES = LoadLanguageRules();

    private sealed record LanguageRule(int Points, bool NearTop, Regex Matcher);

    private sealed record LanguageRuleSet(string Language, IReadOnlyList<LanguageRule> Rules);

    public string GetFileExtension(SupportedLanguages language)
    {
        return language switch
        {
            SupportedLanguages.Javascript => ".js",
            SupportedLanguages.CSharp => ".cs",
            SupportedLanguages.Blazor => ".razor",
            SupportedLanguages.Cpp => ".cpp",
            SupportedLanguages.Java => ".java",
            SupportedLanguages.Python => ".py",
            SupportedLanguages.PHP => ".php",
            SupportedLanguages.Ruby => ".rb",
            SupportedLanguages.C => ".c",
            SupportedLanguages.JSON => ".json",
            SupportedLanguages.HTML => ".html",
            SupportedLanguages.CSS => ".css",
            SupportedLanguages.GO => ".go",
            SupportedLanguages.Markdown => ".md",
            SupportedLanguages.YAML => ".yaml",
            SupportedLanguages.SQL => ".sql",
            SupportedLanguages.Vue => ".vue",
            SupportedLanguages.XML => ".xml",
            SupportedLanguages.Bat => ".bat",
            _ => ".txt"
        };
    }

    public SupportedLanguages DetectLang(string snippet)
    {
        return GetLangScores(snippet).First().Languages;
    }
    public List<ScoresResult> GetLangScores(string snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        var linesOfCode = snippet.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var originalLineCount = linesOfCode.Length;

        bool NearTop(int index)
        {
            if (originalLineCount <= 10) return true;
            return index < Math.Max(1, originalLineCount / 10);
        }

        var sampleInterval = originalLineCount > 500
            ? (int)Math.Ceiling((double)originalLineCount / 500)
            : 1;
        var sampledLines = linesOfCode
            .Select((line, index) => (Line: line, Index: index))
            .Where(item => NearTop(item.Index) || item.Index % sampleInterval == 0)
            .ToArray();

        var results = LANGUAGE_RULES.Select(ruleSet =>
        {
            var language = ruleSet.Language;
            var checkers = ruleSet.Rules;
            if (language == "Unknown") return new ScoresResult()
            {
                Languages = SupportedLanguages.Unknown,
                Scores = 1
            };
            var points = 0;
            foreach (var (line, originalIndex) in sampledLines)
            {
                foreach (var checker in checkers)
                {
                    if (checker.NearTop && !NearTop(originalIndex))
                    {
                        continue;
                    }

                    try
                    {
                        if (checker.Matcher.IsMatch(line))
                        {
                            points += checker.Points;
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        // A pathological line must not prevent all other language rules from scoring.
                    }
                }
            }

            return new ScoresResult()
            {
                Languages = language.ToEnum<SupportedLanguages>()!.Value,
                Scores = points
            };
        }).OrderByDescending(p=>p.Scores).ToList();

        return results;
    }

    private static IReadOnlyList<LanguageRuleSet> LoadLanguageRules()
    {
        var languages = JsonSerializer.Deserialize<Dictionary<string, List<LangOption>>>(LANGUAGE_RULES_JSON)
            ?? throw new InvalidOperationException("Failed to load language detector configuration.");

        return languages
            .Select(pair => new LanguageRuleSet(
                pair.Key,
                pair.Value
                    .Select(option => new LanguageRule(
                        option.points,
                        option.nearTop,
                        new Regex(
                            option.pattern,
                            RegexOptions.Compiled | RegexOptions.CultureInvariant,
                            TimeSpan.FromMilliseconds(250))))
                    .ToArray()))
            .ToArray();
    }

    private const string LANGUAGE_RULES_JSON = """
               {
            "JavaScript": [
                {
                    "pattern": "undefined",
                    "points": 2
                },
                {
                    "pattern": "console\\.log( )*\\(",
                    "points": 2
                },
                {
                    "pattern": "(var|const|let)( )+\\w+( )*=?",
                    "points": 2
                },
                {
                    "pattern": "(('|\").+('|\")( )*|\\w+):( )*[{\\[]",
                    "points": 2
                },
                {
                    "pattern": "===",
                    "points": 1
                },
                {
                    "pattern": "!==",
                    "points": 1
                },
                {
                    "pattern": "function\\*?(( )+[\\$\\w]+( )*\\(.*\\)|( )*\\(.*\\))",
                    "points": 1
                },
                {
                    "pattern": "null",
                    "points": 1
                },
                {
                    "pattern": "\\(.*\\)( )*=>( )*.+",
                    "points": 1
                },
                {
                    "pattern": "(else )?if( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "while( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "(^|\\s)(char|long|int|float|double)( )+\\w+( )*=?",
                    "points": -1
                },
                {
                    "pattern": "(\\w+)( )*\\*( )*\\w+",
                    "points": -1
                },
                {
                    "pattern": "<(\\/)?script( type=('|\")text\\/javascript('|\"))?>",
                    "points": -50
                }
            ],
            "C": [
                {
                    "pattern": "(char|long|int|float|double)( )+\\w+( )*=?",
                    "points": 2
                },
                {
                    "pattern": "malloc\\(.+\\)",
                    "points": 2
                },
                {
                    "pattern": "#include (<|\")\\w+\\.h(>|\")",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "(\\w+)( )*\\*( )*\\w+",
                    "points": 2
                },
                {
                    "pattern": "(\\w+)( )+\\w+(;|( )*=)",
                    "points": 1
                },
                {
                    "pattern": "(\\w+)( )+\\w+\\[.+\\]",
                    "points": 1
                },
                {
                    "pattern": "#define( )+.+",
                    "points": 1
                },
                {
                    "pattern": "NULL",
                    "points": 1
                },
                {
                    "pattern": "void",
                    "points": 1
                },
                {
                    "pattern": "(else )?if( )*\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "while( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "(printf|puts)( )*\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "new \\w+",
                    "points": -1
                },
                {
                    "pattern": "new [A-Z]\\w*( )*\\(.+\\)",
                    "points": 2
                },
                {
                    "pattern": "'.{2,}'",
                    "points": -1
                },
                {
                    "pattern": "var( )+\\w+( )*=?",
                    "points": -1
                }
            ],
            "Cpp": [
                {
                    "pattern": "(char|long|int|float|double)( )+\\w+( )*=?",
                    "points": 2
                },
                {
                    "pattern": "#include( )*(<|\")\\w+(\\.h)?(>|\")",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "using( )+namespace( )+.+( )*;",
                    "points": 2
                },
                {
                    "pattern": "template( )*<.*>",
                    "points": 2
                },
                {
                    "pattern": "std::\\w+",
                    "points": 2
                },
                {
                    "pattern": "(cout|cin|endl)",
                    "points": 2
                },
                {
                    "pattern": "(public|protected|private):",
                    "points": 2
                },
                {
                    "pattern": "nullptr",
                    "points": 2
                },
                {
                    "pattern": "new \\w+(\\(.*\\))?",
                    "points": 1
                },
                {
                    "pattern": "#define( )+.+",
                    "points": 1
                },
                {
                    "pattern": "\\w+<\\w+>",
                    "points": 1
                },
                {
                    "pattern": "class( )+\\w+",
                    "points": 1
                },
                {
                    "pattern": "void",
                    "points": 1
                },
                {
                    "pattern": "(else )?if( )*\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "while( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "\\w*::\\w+",
                    "points": 1
                },
                {
                    "pattern": "'.{2,}'",
                    "points": -1
                },
                {
                    "pattern": "(List<\\w+>|ArrayList<\\w*>( )*\\(.*\\))(( )+[\\w]+|;)",
                    "points": -1
                }
            ],
            "Python": [
                {
                    "pattern": "def( )+\\w+\\(.*\\)( )*:",
                    "points": 2
                },
                {
                    "pattern": "while (.+):",
                    "points": 2
                },
                {
                    "pattern": "from [\\w\\.]+ import (\\w+|\\*)",
                    "points": 2
                },
                {
                    "pattern": "class( )*\\w+(\\(( )*\\w+( )*\\))?( )*:",
                    "points": 2
                },
                {
                    "pattern": "if( )+(.+)( )*:",
                    "points": 2
                },
                {
                    "pattern": "elif( )+(.+)( )*:",
                    "points": 2
                },
                {
                    "pattern": "else:",
                    "points": 2
                },
                {
                    "pattern": "for (\\w+|\\(?\\w+,( )*\\w+\\)?) in (.+):",
                    "points": 2
                },
                {
                    "pattern": "\\w+( )*=( )*\\w+(?!;)(\\n|$)",
                    "points": 1
                },
                {
                    "pattern": "import ([[^\\.]\\w])+",
                    "points": 1,
                    "nearTop": true
                },
                {
                    "pattern": "print((( )*\\(.+\\))|( )+.+)",
                    "points": 1
                },
                {
                    "pattern": "(&{2}|\\|{2})",
                    "points": -1
                }
            ],
            "Java": [
                {
                    "pattern": "System\\.(in|out)\\.\\w+",
                    "points": 2
                },
                {
                    "pattern": "(private|protected|public)( )*\\w+( )*\\w+(( )*=( )*[\\w])?",
                    "points": 2
                },
                {
                    "pattern": "(private|protected|public)( )*\\w+( )*[\\w]+\\(.+\\)",
                    "points": 2
                },
                {
                    "pattern": "(^|\\s)(String)( )+[\\w]+( )*=?",
                    "points": 2
                },
                {
                    "pattern": "(List<\\w+>|ArrayList<\\w*>( )*\\(.*\\))(( )+[\\w]+|;)",
                    "points": 2
                },
                {
                    "pattern": "(public( )*)?class( )*\\w+",
                    "points": 2
                },
                {
                    "pattern": "(\\w+)(\\[( )*\\])+( )+\\w+",
                    "points": 2
                },
                {
                    "pattern": "final( )*\\w+",
                    "points": 2
                },
                {
                    "pattern": "\\w+\\.(get|set)\\(.+\\)",
                    "points": 2
                },
                {
                    "pattern": "new [A-Z]\\w*( )*\\(.+\\)",
                    "points": 2
                },
                {
                    "pattern": "(^|\\s)(char|long|int|float|double)( )+[\\w]+( )*=?",
                    "points": 1
                },
                {
                    "pattern": "(extends|implements)",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "null",
                    "points": 1
                },
                {
                    "pattern": "(else )?if( )*\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "while( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "void",
                    "points": 1
                },
                {
                    "pattern": "const( )*\\w+",
                    "points": -1
                },
                {
                    "pattern": "(\\w+)( )*\\*( )*\\w+",
                    "points": -1
                },
                {
                    "pattern": "'.{2,}'",
                    "points": -1
                },
                {
                    "pattern": "#include( )*(<|\")\\w+(\\.h)?(>|\")",
                    "points": -1,
                    "nearTop": true
                }
            ],
            "HTML": [
                {
                    "pattern": "<!DOCTYPE (html|HTML PUBLIC .+)>",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "<[a-z0-9]+(( )*[\\w]+=('|\").+('|\")( )*)?>.*<\\/[a-z0-9]+>",
                    "points": 2
                },
                {
                    "pattern": "[a-z\\-]+=(\"|').+(\"|')",
                    "points": 2
                },
                {
                    "pattern": "<\\?php",
                    "points": -50
                }
            ],
            "CSharp": [
                {
                    "pattern": "using .+;",
                    "points": 3,
                    "nearTop": true
                },
                {
                    "pattern": "namespace .+",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "public|private|protected",
                    "points": 2
                },
                {
                    "pattern": "virtual|override|sealed",
                    "points": 3
                }
            ],
            "CSS": [
                {
                    "pattern": "[a-z\\-]+:(?!:).+;",
                    "points": 2
                },
                {
                    "pattern": "<(\\/)?style>",
                    "points": -50
                }
            ],
            "Ruby": [
                {
                    "pattern": "(require|include)( )+'\\w+(\\.rb)?'",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "def( )+\\w+( )*(\\(.+\\))?( )*\\n",
                    "points": 2
                },
                {
                    "pattern": "@\\w+",
                    "points": 2
                },
                {
                    "pattern": "\\.\\w+\\?",
                    "points": 2
                },
                {
                    "pattern": "puts( )+(\"|').+(\"|')",
                    "points": 2
                },
                {
                    "pattern": "class [A-Z]\\w*( )*<( )*([A-Z]\\w*(::)?)+",
                    "points": 2
                },
                {
                    "pattern": "attr_accessor( )+(:\\w+(,( )*)?)+",
                    "points": 2
                },
                {
                    "pattern": "\\w+\\.new( )+",
                    "points": 2
                },
                {
                    "pattern": "elsif",
                    "points": 2
                },
                {
                    "pattern": "do( )*\\|(\\w+(,( )*\\w+)?)+\\|",
                    "points": 2
                },
                {
                    "pattern": "for (\\w+|\\(?\\w+,( )*\\w+\\)?) in (.+)",
                    "points": 1
                },
                {
                    "pattern": "nil",
                    "points": 1
                },
                {
                    "pattern": "[A-Z]\\w*::[A-Z]\\w*",
                    "points": 1
                }
            ],
            "Go": [
                {
                    "pattern": "package( )+[a-z]+\\n",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "(import( )*\\(( )*\\n)|(import( )+\"[a-z0-9\\/\\.]+\")",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "if.+err( )*!=( )*nil.+{",
                    "points": 2
                },
                {
                    "pattern": "fmt\\.Print(f|ln)?\\(.*\\)",
                    "points": 2
                },
                {
                    "pattern": "func(( )+\\w+( )*)?\\(.*\\).*{",
                    "points": 2
                },
                {
                    "pattern": "\\w+( )*:=( )*.+[^;\\n]",
                    "points": 2
                },
                {
                    "pattern": "(}( )*else( )*)?if[^\\(\\)]+{",
                    "points": 2
                },
                {
                    "pattern": "(var|const)( )+\\w+( )+[\\w\\*]+(\\n|( )*=|$)",
                    "points": 2
                },
                {
                    "pattern": "[a-z]+\\.[A-Z]\\w*",
                    "points": 1
                },
                {
                    "pattern": "nil",
                    "points": 1
                },
                {
                    "pattern": "'.{2,}'",
                    "points": -1
                }
            ],
            "PHP": [
                {
                    "pattern": "<\\?php",
                    "points": 2
                },
                {
                    "pattern": "\\$\\w+",
                    "points": 2
                },
                {
                    "pattern": "use( )+\\w+(\\\\\\w+)+( )*;",
                    "points": 2,
                    "nearTop": true
                },
                {
                    "pattern": "\\$\\w+\\->\\w+",
                    "points": 2
                },
                {
                    "pattern": "(require|include)(_once)?( )*\\(?( )*('|\").+\\.php('|\")( )*\\)?( )*;",
                    "points": 2
                },
                {
                    "pattern": "echo( )+('|\").+('|\")( )*;",
                    "points": 1
                },
                {
                    "pattern": "NULL",
                    "points": 1
                },
                {
                    "pattern": "new( )+((\\\\\\w+)+|\\w+)(\\(.*\\))?",
                    "points": 1
                },
                {
                    "pattern": "function(( )+[\\$\\w]+\\(.*\\)|( )*\\(.*\\))",
                    "points": 1
                },
                {
                    "pattern": "(else)?if( )+\\(.+\\)",
                    "points": 1
                },
                {
                    "pattern": "\\w+::\\w+",
                    "points": 1
                },
                {
                    "pattern": "===",
                    "points": 1
                },
                {
                    "pattern": "!==",
                    "points": 1
                },
                {
                    "pattern": "(^|\\s)(var|char|long|int|float|double)( )+\\w+( )*=?",
                    "points": -1
                }
            ],
            "JSON": [
                {
                    "pattern": "^\\s*\\{.*\\}\\s*$",
                    "points": 10
                }
            ],
            "YAML": [
                {
                    "pattern": "^\\s+- ",
                    "points": 1
                },
                {
                    "pattern": "^\\s+\\w+: [^'\"]+",
                    "points": 1
                },
                {
                    "pattern": "^\\w+:(\\r\\n|\\n)\\s+",
                    "points": 1
                },
                {
                    "pattern": "^\\s*<<: \\*",
                    "points": 2
                },
                {
                    "pattern": "^\\w+:\\s*&\\w+(\\r\\n|\\n)\\s+",
                    "points": 2
                }
            ],
            "Markdown": [
                {
                    "pattern": "^#+ .*$",
                    "points": 1
                },
                {
                    "pattern": "^> ?.*$",
                    "points": 5
                },
                {
                    "pattern": "^`.*`$",
                    "points": 5
                },
                {
                    "pattern": "\\[[^\\[\\]]+\\]\\(https?[^()]+\\)",
                    "points": 5
                },
                {
                    "pattern": "^!\\[[^\\[\\]]+\\]\\(https?[^()]+\\)",
                    "points": 10
                },
                {
                    "pattern": "(\\*|\\*\\*|\\*\\*\\*|_|__|___|~~)[^\\r\\n]*\\1",
                    "points": 2
                },
                {
                    "pattern": "^[-* ]{4,}$",
                    "points": 4
                }
            ],
            "Unknown": []
        }
        """;
}
