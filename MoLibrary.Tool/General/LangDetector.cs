using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.General;
/// <summary>
/// Programming language detector
/// </summary>
public class LangDetector
{
    public class LangOption
    {
        public string pattern { get; set; }
        public int points { get; set; }
        public bool nearTop { get; set; }
    }

    public class Root
    {
        public Dictionary<string, List<LangOption>> Languages;
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
        var languages = JsonSerializer.Deserialize<Dictionary<string, List<LangOption>>>(JsonConfig);
        var linesOfCode = snippet.RegexReplace("\r\n?", "\n").RegexReplace("\n{2,}", "\n").Split('\n');

        bool NearTop(int index)
        {
            if (linesOfCode.Length <= 10) return true;
            return index < linesOfCode.Length / 10;
        }

        if (linesOfCode.Length > 500)
        {
            linesOfCode = linesOfCode
                .Where((lineOfCode, index) => NearTop(index) || index % (int)Math.Ceiling((double)linesOfCode.Length / 500) == 0)
                .ToArray();
        }

        var pairs = languages.Select(pair => new { language = pair.Key, checkers = pair.Value }).ToList();

        var useNearTop = linesOfCode.Length > 500;
        var results = pairs.Select(pair =>
        {
            var language = pair.language;
            var checkers = pair.checkers;
            if (language == "Unknown") return new ScoresResult()
            {
                Languages = SupportedLanguages.Unknown,
                Scores = 1
            };
            var pointsList = linesOfCode.Select((lineOfCode, index) =>
            {
                
                var relevantCheckers = !useNearTop ? checkers:NearTop(index)
                    ? checkers.Where(checker => checker.nearTop).ToList()
                    : checkers.Where(checker => !checker.nearTop).ToList();

                return relevantCheckers
                    .Where(checker => Regex.IsMatch(lineOfCode, checker.pattern))
                    .Sum(checker => checker.points);
            }).ToList();

            var points = pointsList.Sum();
            return new ScoresResult()
            {
                Languages = language.ToEnum<SupportedLanguages>()!.Value,
                Scores = points
            };
        }).OrderByDescending(p=>p.Scores).ToList();

        return results;
    }


    public static string JsonConfig = """
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
                },
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