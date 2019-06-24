using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Sgml_parser
{
    public class SgmlConverter
    {
        private readonly ILogger<SgmlConverter> logger;

        public SgmlConverter(ILogger<SgmlConverter> logger)
        {
            this.logger = logger;
        }

        public string ConvertToJson(Stream inputStream, string[] attachmemtTags)
        {
            using (TextReader reader = new StreamReader(inputStream))
            {
                return JsonFromXml(reader, attachmemtTags);
            }
        }

        public string ConvertToJson(string input, string[] attachmemtTags)
        {
            using (TextReader reader = new StringReader(input))
            {
                return JsonFromXml(reader, attachmemtTags);
            }
        }

        private string ConvertBinaryToBase64(TextReader reader, string[] attachmemtTags)
        {
            string value = reader.ReadToEnd();
            foreach(string item in attachmemtTags)
            {
                value = Regex.Replace(value, @"<"+item+">(?>.*?</"+item+">)", m =>
                {
                    logger.LogTrace("Binary content converted to base64");
                    string valueToReplace = m.Groups[0].Value;
                    valueToReplace = valueToReplace.Substring(("<" + item + ">").Length);
                    valueToReplace = valueToReplace.Substring(0, valueToReplace.Length - ("</" + item + ">").Length);
                    var buffer = Encoding.UTF8.GetBytes(valueToReplace);
                    return "<"+ item + ">" + Convert.ToBase64String(buffer) + "</" + item + ">";
                }, RegexOptions.Singleline);
            }

            return value;
        }

        private string JsonFromXml(TextReader reader, string[] attachmemtTags)
        {
            string preProcessed = ConvertBinaryToBase64(reader, attachmemtTags);
            using (TextReader readerAfter = new StringReader(preProcessed))
            {
                XmlDocument doc = XmlFromSgml(readerAfter);
                string json = JsonConvert.SerializeXmlNode(doc);
                return json;
            }
        }


        private XmlDocument XmlFromSgml(TextReader textReader)
        {
            Stack<string> stack = new Stack<string>();
            TextWriter logWriter = new StringWriter();
            try
            {
                Sgml.SgmlReader reader = new Sgml.SgmlReader();
                reader.DocType = "HTML";
                reader.WhitespaceHandling = WhitespaceHandling.All;
                reader.CaseFolding = Sgml.CaseFolding.ToLower;
                reader.InputStream = textReader;
                reader.ErrorLog = logWriter;

                StringWriter writer = new StringWriter();
                XmlWriter xmlWriter = null;

                try
                {
                    xmlWriter = new XmlTextWriter(writer);

                    bool rootElement = true;
                    string lastElementName = "";
                    while ((reader.Read()) && !reader.EOF)
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                try
                                {
                                    xmlWriter.WriteStartElement(reader.LocalName);
                                    xmlWriter.WriteAttributes(reader, true);
                                    if (reader.IsEmptyElement)
                                    {
                                        xmlWriter.WriteEndElement();
                                        lastElementName = "";
                                    }
                                    else
                                    {
                                        lastElementName = reader.LocalName;
                                    }
                                }
                                catch (Exception exc)
                                {
                                    logger.LogError(exc, lastElementName);
                                }


                                break;

                            case XmlNodeType.Text:
                                if (string.IsNullOrEmpty(lastElementName))
                                {
                                    string text = reader.Value;
                                    xmlWriter.WriteString(text);
                                }
                                else
                                {
                                    if (rootElement)
                                    {
                                        rootElement = false;
                                        stack.Push(".");
                                    }
                                    else
                                    {
                                        xmlWriter.WriteElementString("value", reader.Value);
                                        xmlWriter.WriteEndElement();
                                        stack.Push(lastElementName);
                                    }
                                    lastElementName = "";
                                }
                                break;

                            case XmlNodeType.EndElement:
                                if (stack.Peek() == reader.LocalName)
                                {
                                    stack.Pop();
                                    break;
                                }

                                logger.LogTrace(reader.LocalName);
                                try
                                {
                                    xmlWriter.WriteEndElement();
                                    lastElementName = "";
                                }
                                catch (Exception exc)
                                {
                                    logger.LogError(exc, lastElementName);
                                }
                                break;


                        }
                    }
                }
                finally
                {
                    if (xmlWriter != null)
                    {
                        xmlWriter.Close();
                    }
                }


                string xml = writer.ToString();

                XmlDocument doc = new XmlDocument();

                doc.PreserveWhitespace = true;

                doc.LoadXml(xml);
                return doc;
            }
            finally
            {
                string str = logWriter.ToString();
                if (string.IsNullOrEmpty(str))
                    logger.LogError(str);
                logWriter.Dispose();
            }
        }
    }
}
