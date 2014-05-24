using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace GithubWikiDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            var xml = File.ReadAllText(args[0]);
            var doc = XDocument.Parse(xml);
            var md = doc.Root.ToMarkDown();
            Console.WriteLine(md);
        }
    }

    static class XmlToMarkdown
    {
        static IEnumerable<string> DocToMarkDown(XNode e)
        {
            var el = (XElement) e;
            var members = el.Element("members").Elements("member");
            return new[]
                {
                    el.Element("assembly").Element("name").Value,
                    string.Join("", members.Where(x => x.Attribute("name").Value.StartsWith("F:")).ToMarkDown()),
                    string.Join("", members.Where(x => x.Attribute("name").Value.StartsWith("M:")).ToMarkDown()),
                    string.Join("", members.Where(x => x.Attribute("name").Value.StartsWith("E:")).ToMarkDown())
                };
        }

        internal static string ToMarkDown(this XNode e)
        {
            var templates = new Dictionary<string, string>
                {
                    {"doc", "## {0} ##\n\n## Fields\n\n{1}\n\n## Methods\n\n{2}\n\n## Events\n\n{3}\n\n"},
                    {"field", "### {0}\n\n{1}\n\n"},
                    {"method", "### {0}\n\n{1}\n\n"},
                    {"event", "### {0}\n\n{1}\n\n"},
                    {"summary", "{0}\n\n"},
                    {"remarks", "**remarks**\n\n{0}\n\n"},
                    {"example", "**example**\n\n{0}\n\n"},
                    {"see", "[{1}]({0})"},
                    {"param", "_{0}_: {1}" },
                    {"exception", "_{0}_: {1}\n\n" },
                    {"returns", "Returns: {0}\n\n"},
                    {"none", ""}
                };
            var d = new Func<string, XElement, string[]>((att, node) => new[]
                {
                    node.Attribute(att).Value, 
                    node.Nodes().ToMarkDown()
                });
            var methods = new Dictionary<string, Func<XElement, IEnumerable<string>>>
                {
                    {"doc", DocToMarkDown},
                    {"field", x=> d("name", x)},
                    {"method",x=>d("name", x)},
                    {"event", x=>d("name", x)},
                    {"summary", x=> new[]{ x.Nodes().ToMarkDown() }},
                    {"remarks", x => new[]{x.Nodes().ToMarkDown()}},
                    {"example", x => new[]{x.Value.ToCodeBlock()}},
                    {"see", x=>d("cref",x)},
                    {"param", x => d("name", x) },
                    {"exception", x => d("cref", x) },
                    {"returns", x => new[]{x.Nodes().ToMarkDown()}},
                    {"none", x => new string[0]}
                };

            string name;
            if(e.NodeType== XmlNodeType.Element)
            {
                var el = (XElement) e;
                name = el.Name.LocalName;
                if (name == "member")
                {
                    switch (el.Attribute("name").Value[0])
                    {
                        case 'F': name = "field";  break;
                        case 'E': name = "event";  break;
                        case 'M': name = "method"; break;
                        default:  name = "none";   break;
                    }
                }
                var vals = methods[name](el).ToArray();
                string str="";
                switch (vals.Length)
                {
                    case 1: str= string.Format(templates[name], vals[0]);break;
                    case 2: str= string.Format(templates[name], vals[0],vals[1]);break;
                    case 3: str= string.Format(templates[name], vals[0],vals[1],vals[2]);break;
                    case 4: str= string.Format(templates[name], vals[0], vals[1], vals[2], vals[3]);break;
                }

                return str;
            }

            if(e.NodeType==XmlNodeType.Text)
                return Regex.Replace( ((XText)e).Value.Replace('\n', ' '), @"\s+", " ");

            return "";
        }

        internal static string ToMarkDown(this IEnumerable<XNode> es)
        {
            return es.Aggregate("", (current, x) => current + x.ToMarkDown());
        }

        static string ToCodeBlock(this string s)
        {
            var lines = s.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
            return string.Join("\n",lines.Select(x => new string(x.SkipWhile((y, i) => i < blank).ToArray())));
        }
    }
}
