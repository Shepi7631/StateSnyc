using System.Collections.Generic;
using System.Globalization;
using Pathfinding.Data;

namespace NavMeshDemo
{
    internal static class NavMeshJsonLoader
    {
        public static (Polygon Boundary, List<Polygon> Obstacles) Load(string json)
        {
            var root = (Dictionary<string, object>)_TinyJson.Parse(json);
            var boundary = _ToPolygon((List<object>)root["boundary"]);
            var obstacles = new List<Polygon>();
            if (root.TryGetValue("obstacles", out object obsObj))
            {
                foreach (var obs in (List<object>)obsObj)
                    obstacles.Add(_ToPolygon((List<object>)obs));
            }
            return (boundary, obstacles);
        }

        private static Polygon _ToPolygon(List<object> points)
        {
            var verts = new List<Vec2>(points.Count);
            foreach (var p in points)
            {
                var pair = (List<object>)p;
                verts.Add(new Vec2((float)pair[0], (float)pair[1]));
            }
            return new Polygon(verts);
        }
    }

    internal static class _TinyJson
    {
        public static object Parse(string json)
        {
            int pos = 0;
            _SkipWs(json, ref pos);
            return _ParseValue(json, ref pos);
        }

        private static object _ParseValue(string s, ref int pos)
        {
            _SkipWs(s, ref pos);
            char c = s[pos];
            if (c == '{') return _ParseObject(s, ref pos);
            if (c == '[') return _ParseArray(s, ref pos);
            if (c == '"') return _ParseString(s, ref pos);
            return _ParseNumber(s, ref pos);
        }

        private static Dictionary<string, object> _ParseObject(string s, ref int pos)
        {
            var dict = new Dictionary<string, object>();
            pos++;
            _SkipWs(s, ref pos);
            if (s[pos] == '}')
            {
                pos++;
                return dict;
            }
            while (true)
            {
                _SkipWs(s, ref pos);
                string key = _ParseString(s, ref pos);
                _SkipWs(s, ref pos);
                pos++;
                object value = _ParseValue(s, ref pos);
                dict[key] = value;
                _SkipWs(s, ref pos);
                if (s[pos] == ',')
                {
                    pos++;
                    continue;
                }
                pos++;
                return dict;
            }
        }

        private static List<object> _ParseArray(string s, ref int pos)
        {
            var list = new List<object>();
            pos++;
            _SkipWs(s, ref pos);
            if (s[pos] == ']')
            {
                pos++;
                return list;
            }
            while (true)
            {
                _SkipWs(s, ref pos);
                list.Add(_ParseValue(s, ref pos));
                _SkipWs(s, ref pos);
                if (s[pos] == ',')
                {
                    pos++;
                    continue;
                }
                pos++;
                return list;
            }
        }

        private static string _ParseString(string s, ref int pos)
        {
            pos++;
            int start = pos;
            while (s[pos] != '"')
                pos++;
            string result = s.Substring(start, pos - start);
            pos++;
            return result;
        }

        private static float _ParseNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (!(char.IsDigit(c) || c == '-' || c == '.' || c == 'e' || c == 'E' || c == '+'))
                    break;
                pos++;
            }
            return float.Parse(s.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }

        private static void _SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                pos++;
        }
    }
}
