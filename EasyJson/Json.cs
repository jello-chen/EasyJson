using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyJson
{
    public class Json
    {
        private static readonly Dictionary<int, JsonItem> cache = new Dictionary<int, JsonItem>();

        public static JsonItem Parse(string json)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(json));
            Contract.Assert(!string.IsNullOrWhiteSpace(json));
            int hashCode = json.GetHashCode();
            if (cache.ContainsKey(hashCode))
            {
                return cache[hashCode];
            }
            else
            {
                if (cache.Count > 0)
                    cache.Clear();
                var jsonItem = ParseImpl(new StringReader(json));
                cache.Add(hashCode, jsonItem);
                return jsonItem;
            }
        }

        public static JsonItem ParseFromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    return ParseImpl(sr);
                }
            }
        }

        public static JsonItem ParseFromStream(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return ParseImpl(sr);
            }
        }

        private static JsonItem ParseImpl(TextReader reader)
        {
            Debug.Assert(reader != null);
            Contract.Assert(reader != null);

            var stack = new Stack<JsonItem>();
            var token = string.Empty;
            var tokenName = string.Empty;
            var isEnterQuote = false;
            JsonItem jsonItem = null;
            var lineNum = 1;
            var charIndex = 1;
            var i = 0;

            using (reader as IDisposable)
            {
                var cb = new CharBuffer(reader, 1024);
                char next = '\uffff';
                while ((next = cb.Next()) != '\uffff')
                {
                    switch (next)
                    {
                        case '{':
                        case '[':
                            if (isEnterQuote)
                            {
                                token += next;
                                break;
                            }
                            JsonItem item = null;
                            if (next == '{') item = new JsonObject();
                            else item = new JsonArray();
                            stack.Push(item);
                            if (jsonItem != null)
                            {
                                if (jsonItem is JsonArray)
                                {
                                    jsonItem.Add(stack.Peek());
                                }
                                else if (tokenName != "")
                                {
                                    jsonItem.Add(tokenName, stack.Peek());
                                }
                            }
                            tokenName = "";
                            token = "";
                            jsonItem = stack.Peek();
                            break;
                        case '"':
                            isEnterQuote ^= true;
                            break;
                        case ':':
                            if (isEnterQuote)
                            {
                                token += next;
                                break;
                            }
                            tokenName = token;
                            token = "";
                            break;
                        case ',':
                            if (isEnterQuote)
                            {
                                token += next;
                                break;
                            }
                            if (token != "")
                            {
                                if (jsonItem is JsonArray)
                                {
                                    jsonItem.Add(token);
                                }
                                else if (tokenName != "")
                                {
                                    jsonItem.Add(tokenName, token);
                                }
                            }
                            tokenName = "";
                            token = "";
                            break;
                        case ']':
                        case '}':
                            if (isEnterQuote)
                            {
                                token += next;
                                break;
                            }
                            if (stack.Count == 0)
                                throw new Exception(string.Format("Parse error on line {0} char {1}:Expecting 'EOF'", lineNum, charIndex));
                            stack.Pop();
                            if (token != "")
                            {
                                if (jsonItem is JsonArray)
                                {
                                    jsonItem.Add(token);
                                }
                                else if (tokenName != "")
                                {
                                    jsonItem.Add(tokenName, token);
                                }
                            }
                            tokenName = "";
                            token = "";
                            if (stack.Count > 0)
                                jsonItem = stack.Peek();
                            break;
                        case '\r':
                        case '\n':
                            lineNum++;
                            charIndex = 1;
                            break;
                        case ' ':
                        case '\t':
                            if (isEnterQuote)
                                token += next;
                            break;
                        case '\\':
                            if (isEnterQuote)
                            {
                                var c = cb.Next();
                                switch (c)
                                {
                                    case 't': token += '\t'; break;
                                    case 'r': token += '\r'; break;
                                    case 'n': token += '\n'; break;
                                    case 'b': token += '\b'; break;
                                    case 'f': token += '\f'; break;
                                    case 'u':
                                        var s = cb.NextString(4);
                                        token += (char)int.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);
                                        i += 4;
                                        break;
                                    default:
                                        token += c;
                                        break;
                                }
                            }
                            break;
                        default:
                            token += next;
                            break;
                    }
                }
                if (isEnterQuote)
                {
                    throw new Exception("Parse error: Quotation marks come out a missing match");
                }
                return jsonItem;
            }
        }
    }

    public class CharBuffer
    {
        private readonly char[] buffer;
        private readonly TextReader reader;
        private int charPosition = 1;
        private int pos;
        private readonly int bufferSize;
        private readonly int doubleBufferSize;

        public CharBuffer(TextReader reader, int bufferSize = 8)
        {
            this.reader = reader;
            this.bufferSize = bufferSize;
            this.buffer = new char[2 * bufferSize];
            this.doubleBufferSize = 2 * bufferSize;
            Flush(0, doubleBufferSize);
        }

        public char Lookahead(int index = 0)
        {
            if (index < 0 || index > bufferSize)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (pos >= doubleBufferSize)
            {
                pos = 0;
                Flush(bufferSize, doubleBufferSize);
            }
            return buffer[(pos + index) % doubleBufferSize];
        }

        public bool Check(IEnumerable<char> list)
        {
            var p = 0;
            return list.All(i => i.Equals(Lookahead(p++)));
        }

        public char Next()
        {
            if (pos == bufferSize)
            {
                Flush(0, bufferSize);
                charPosition++;
                return buffer[pos++];
            }
            if (pos >= doubleBufferSize)
            {
                pos = 0;
                Flush(bufferSize, doubleBufferSize);
                charPosition++;
                return buffer[pos++];
            }
            charPosition++;
            return buffer[pos++];
        }

        public string NextString(int count)
        {
            var charArray = new char[count];
            for (int i = 0; i < count; i++)
            {
                charArray[i] = Next();
            }
            return new string(charArray);
        }

        public void Next(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Next();
            }
        }

        private void Flush(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                buffer[i] = unchecked((char)reader.Read());
                if (buffer[i] == unchecked((char)-1)) break;
            }
        }
    }

    /// <summary>
    /// Json Item
    /// </summary>
    public class JsonItem : DynamicObject
    {
        public virtual JsonItem this[int index]
        {
            get { return null; }
            set { }
        }
        public virtual JsonItem this[string name]
        {
            get { return null; }
            set { }
        }
        public virtual string Value { get { return ""; } set { } }
        public virtual int Count
        {
            get { return 0; }
        }

        public virtual ICollection<string> Keys { get; }
        public virtual ICollection<JsonItem> Values { get; }

        public virtual int IntegerValue
        {
            get
            {
                int v = 0;
                if (int.TryParse(Value, out v))
                    return v;
                return 0;
            }
            set { Value = value.ToString(); }
        }
        public virtual float FloatValue
        {
            get
            {
                float v = 0.0f;
                if (float.TryParse(Value, out v))
                    return v;
                return 0.0f;
            }
            set
            {
                Value = value.ToString();
            }
        }
        public virtual double DoubleValue
        {
            get
            {
                double v = 0.0;
                if (double.TryParse(Value, out v))
                    return v;
                return 0.0;
            }
            set
            {
                Value = value.ToString();
            }
        }
        public virtual bool BooleanValue
        {
            get
            {
                bool v = false;
                if (bool.TryParse(Value, out v))
                    return v;
                return !string.IsNullOrEmpty(Value);
            }
            set
            {
                Value = (value) ? "true" : "false";
            }
        }

        public virtual JsonArray JsonArray
        {
            get
            {
                return this as JsonArray;
            }
        }

        public virtual JsonObject JsonObject
        {
            get
            {
                return this as JsonObject;
            }
        }

        public virtual void PrintTree(TextWriter log, string indent = " ", bool last = true)
        {

        }

        public static implicit operator JsonItem(string json)
        {
            return new JsonValue(json);
        }

        public static implicit operator string(JsonItem item)
        {
            return item == null ? null : item.Value;
        }

        public virtual void Add(string token, JsonItem item) { }

        public virtual void Add(JsonItem item)
        {
            Add(string.Empty, item);
        }
        public virtual JsonItem Remove(string token) { return null; }
        public virtual JsonItem Remove(int index) { return null; }

        internal static string Escape(string text)
        {
            string result = "";
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\': result += "\\\\"; break;
                    case '\"': result += "\\\""; break;
                    case '\n': result += "\\n"; break;
                    case '\r': result += "\\r"; break;
                    case '\t': result += "\\t"; break;
                    case '\b': result += "\\b"; break;
                    case '\f': result += "\\f"; break;
                    default: result += c; break;
                }
            }
            return result;
        }

        public virtual IEnumerable<JsonItem> Items
        {
            get { return Enumerable.Empty<JsonItem>(); }
        }
    }

    public class JsonObject : JsonItem, IEnumerable
    {
        private readonly IDictionary<string, JsonItem> _dict = new Dictionary<string, JsonItem>();

        #region IEnumerable Interface Implementation
        public IEnumerator GetEnumerator()
        {
            foreach (var pair in _dict)
                yield return pair;
        }
        #endregion

        public override int Count
        {
            get
            {
                return _dict.Count;
            }
        }

        public override ICollection<string> Keys
        {
            get
            {
                return _dict.Keys;
            }
        }

        public override ICollection<JsonItem> Values
        {
            get
            {
                return _dict.Values;
            }
        }

        public override JsonItem this[int index]
        {
            get
            {
                return _dict.ElementAt(index).Value;
            }
            set
            {
                var key = _dict.ElementAt(index).Key;
                _dict[key] = value;
            }
        }

        public override JsonItem this[string name]
        {
            get
            {
                return _dict[name];
            }

            set
            {
                _dict[name] = value;
            }
        }

        public override void Add(string token, JsonItem item)
        {
            if (!string.IsNullOrEmpty(token))
            {
                if (_dict.ContainsKey(token))
                {
                    _dict[token] = item;
                }
                else
                {
                    _dict.Add(token, item);
                }
            }
            else
            {
                _dict.Add(Guid.NewGuid().ToString(), item);
            }
        }

        public override JsonItem Remove(int index)
        {
            var item = _dict.ElementAt(index);
            _dict.Remove(item.Key);
            return item.Value;
        }   

        public override void PrintTree(TextWriter log, string indent = " ", bool last = true)
        {
            if (log != null)
            {
                log.Write(indent);
                if (last)
                {
                    log.Write("\\-");
                    indent += "  ";
                }
                else
                {
                    log.Write("|-");
                    indent += "| ";
                }
                log.Write(Value);
                for (int i = 0; i < _dict.Count; i++)
                {
                    _dict.ElementAt(i).Value.PrintTree(log, indent, i == _dict.Count - 1);
                }
            }
        }
        public override string ToString()
        {
            string result = "{";
            foreach (KeyValuePair<string, JsonItem> pair in _dict)
            {
                if (result.Length > 2)
                    result += ", ";
                result += "\"" + Escape(pair.Key) + "\":" + pair.Value.ToString();
            }
            result += "}";
            return result;
        }

        public override bool TryDeleteMember(DeleteMemberBinder binder)
        {
            if (binder == null) throw new ArgumentNullException("binder");
            return _dict.Remove(binder.Name);
        }
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes == null) throw new ArgumentNullException("indexes");
            if (indexes.Length == 1)
            {
                result = _dict[(string)indexes[0]];
                return true;
            }
            result = null;
            return true;
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            JsonItem item;
            if (_dict.TryGetValue(binder.Name, out item))
            {
                result = item;
                return true;
            }
            result = null;
            return true;
        }
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (indexes == null) throw new ArgumentNullException("indexes");
            if (indexes.Length == 1)
            {
                if (value is string)
                    _dict[(string)indexes[0]] = value.ToString();
                if (value is JsonItem)
                    _dict[(string)indexes[0]] = (JsonItem)value;
                return true;
            }
            return base.TrySetIndex(binder, indexes, value);
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (binder == null) throw new ArgumentNullException("binder");
            _dict[binder.Name] = (JsonItem)value;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var key in Keys)
                yield return key;
        }

        public override IEnumerable<JsonItem> Items
        {
            get
            {
                return Values;
            }
        }
    }

    public class JsonArray : JsonItem, IEnumerable
    {
        private readonly List<JsonItem> _list = new List<JsonItem>();

        public override int Count
        {
            get
            {
                return _list.Count;
            }
        }

        public override JsonItem this[int index]
        {
            get
            {
                return _list[index];
            }

            set
            {
                if (index < 0 || index >= _list.Count)
                    _list.Add(value);
                else
                    _list[index] = value;
            }
        }

        public override void Add(string token, JsonItem item)
        {
            _list.Add(item);
        }

        public override void Add(JsonItem item)
        {
            _list.Add(item);
        }

        public override JsonItem Remove(int index)
        {
            var item = _list[index];
            _list.RemoveAt(index);
            return item;
        }

        #region IEnumerable Interface Implementation
        public IEnumerator GetEnumerator()
        {
            foreach (var item in _list)
                yield return item;
        }
        #endregion

        public override void PrintTree(TextWriter log, string indent = " ", bool last = true)
        {
            if (log != null)
            {
                log.Write(indent);
                if (last)
                {
                    log.Write("\\-");
                    indent += "  ";
                }
                else
                {
                    log.Write("|-");
                    indent += "| ";
                }
                log.Write(Value);
                for (int i = 0; i < _list.Count; i++)
                {
                    _list[i].PrintTree(log, indent, i == _list.Count - 1);
                }
            }
        }

        public override string ToString()
        {
            string result = "[ ";
            foreach (var item in _list)
            {
                if (result.Length > 2)
                    result += ", ";
                result += item.ToString();
            }
            result += " ]";
            return result;
        }

        public override IEnumerable<JsonItem> Items
        {
            get
            {
                return _list;
            }
        }
    }

    public class JsonValue : JsonItem
    {
        private string _value;
        public override string Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        public JsonValue(string value)
        {
            _value = value;
        }

        public JsonValue(int intValue)
        {
            IntegerValue = intValue;
        }
        public JsonValue(float floatValue)
        {
            FloatValue = floatValue;
        }
        public JsonValue(double doubleValue)
        {
            DoubleValue = doubleValue;
        }
        public JsonValue(bool boolValue)
        {
            BooleanValue = boolValue;
        }

        public override void PrintTree(TextWriter log, string indent = " ", bool last = true)
        {
            if (log != null)
            {
                if (last)
                {
                    log.Write("\\-");
                }
                else
                {
                    log.Write("|-");
                }
                log.WriteLine(Value);
            }
        }

        public override string ToString()
        {
            return "\"" + Escape(_value) + "\"";
        }
    }
}
