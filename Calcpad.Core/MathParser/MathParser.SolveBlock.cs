﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    public partial class MathParser
    {
        private class SolveBlock
        {
            private readonly MathParser _parser;
            internal enum SolverTypes
            {
                None,
                Find,
                Root,
                Sup,
                Inf,
                Area,
                Slope,
                Repeat,
                Sum,
                Product,
                Error
            }

            private static readonly Dictionary<string, SolverTypes> Definitions = new()
            {
                { "$find", SolverTypes.Find },
                { "$root", SolverTypes.Root },
                { "$sup", SolverTypes.Sup },
                { "$inf", SolverTypes.Inf },
                { "$area", SolverTypes.Area },
                { "$slope", SolverTypes.Slope },
                { "$repeat", SolverTypes.Repeat },
                { "$sum", SolverTypes.Sum },
                { "$product", SolverTypes.Product }
            };

            private static readonly string[] TypeNames =
            {
                string.Empty,
                "$Find",
                "$Root",
                "$Sup",
                "$Inf",
                "$Area",
                "$Slope",
                "$Repeat",
                "∑",
                "∏",
                "$Error"
            };

            private readonly StringBuilder _stringBuilder = new();
            private readonly SolverTypes _type;
            private SolverItem[] _items;
            private string Script { get; }
            private Func<Value> _function;
            internal Value Result { get; private set; }

            internal SolveBlock(string script, SolverTypes type, MathParser parser)
            {
                Script = script;
                _type = type;
                _parser = parser;
                Parse();
            }

            internal static SolverTypes GetSolverType(string keyword)
            {
                var s = keyword.Trim().ToLowerInvariant();
                if (Definitions.ContainsKey(s))
                    return Definitions[s];

                return SolverTypes.Error;
            }

            private static string TypeName(SolverTypes st)
            {
                var i = (int)st;
                if (i >= 0 && i < (int)SolverTypes.Error)
                    return TypeNames[i];

                return TypeNames[(int)SolverTypes.Error];
            }

            private void Parse()
            {
                var n = 3;
                if (_type == SolverTypes.Slope)
                    n = 2;

                const string delimiters = "@=:";
                _items = new SolverItem[5];
                int current = 0, bracketCounter = 0;
                _stringBuilder.Clear();
                for (int i = 0, len = Script.Length; i < len; ++i)
                {
                    var c = Script[i];
                    if (c == '{')
                        ++bracketCounter;
                    else if (c == '}')
                        --bracketCounter;

                    if (bracketCounter == 0 && current < n && c == delimiters[current])
                    {
                        _items[current].Input = _stringBuilder.ToString();
                        _stringBuilder.Clear();
                        ++current;
                    }
                    else
                        _stringBuilder.Append(c);
                }

                _items[current].Input = _stringBuilder.ToString();
                var targetUnits = _parser._targetUnits;
                var allowAssignment = _type == SolverTypes.Repeat || _type == SolverTypes.Root;
                for (int i = 0; i <= n; ++i)
                {
                    if (string.IsNullOrWhiteSpace(_items[i].Input))
#if BG
                        throw new MathParserException($"Липсва разделител \"{delimiters[i]}\" в команда за числени методи {{{Script}}}.");
#else
                        throw new MathParserException($"Missing delimiter \"{delimiters[i]}\" in solver command {{{Script}}}.");
#endif
                    _items[i].Input = _items[i].Input.Trim();
                    if (i == 0 && _type == SolverTypes.Root)
                    {
                        var s = _items[0].Input.Split('=');
                        _items[0].Input = s[0];
                        if (s.Length == 2)
                        {
                            _items[4].Input = s[1];
                            n = 4;
                        }
                        else if (s.Length > 2)
                        {
#if BG
                            throw new MathParserException($"Повече от един оператор '=' в '{string.Join('=', s)}'.");
#else
                            throw new MathParserException($"More than one operators '=' in '{string.Join('=', s)}'.");
#endif
                        }
                    }
                    _parser.Parse(_items[i].Input, i == 0 && allowAssignment);
                    _items[i].Rpn = _parser._rpn;
                    _items[i].Html = _parser.ToHtml();
                    _items[i].Xml = _parser.ToXml();
                }
                if (_type == SolverTypes.Inf || _type == SolverTypes.Sup)
                {
                    var s = _items[1].Input + (_type == SolverTypes.Sup ? "_sup" : "_inf");
                    _parser.SetVariable(s, new Value(double.NaN));
                }
                _parser._targetUnits = targetUnits;
            }

            internal void Compile(MathParser parser)
            {
                if (_function is not null)
                    return;

                var vt = (VariableToken)_items[1].Rpn[0];
                Parameter[] parameters = { new(vt.Content) };
                parameters[0].SetValue(Value.Zero);
                vt.Variable = parameters[0];
                parser.BindParameters(parameters, _items[0].Rpn);
                _function = parser.CompileRpn(_items[0].Rpn);
            }

            internal void BindParameters(Parameter[] parameters, MathParser parser)
            {
                if (parser.IsEnabled)
                {
                    parser.BindParameters(parameters, _items[0].Rpn);
                    parser.BindParameters(parameters, _items[2].Rpn);
                    if (_items[3].Rpn is not null)
                        parser.BindParameters(parameters, _items[3].Rpn);

                    if (_items[4].Rpn is not null)
                        parser.BindParameters(parameters, _items[4].Rpn);
                }
            }

            internal Value Calculate()
            {
                var parserRpn = _parser._rpn;
                var parserUnits = _parser.Units;
                var targetUnits = _parser._targetUnits;
                var isPlotting = _parser.IsPlotting;
                _parser._isSolver = true;
                var t = (VariableToken)_items[1].Rpn[0];
                var x = t.Variable;
                _parser._rpn = _items[2].Rpn;
                _parser._targetUnits = null;
                var x1 = _parser.CalculateReal();
                double x2 = 0.0, y = 0.0;
                var ux1 = _parser.Units;
                if (_type != SolverTypes.Slope)
                {
                    _parser._rpn = _items[3].Rpn;
                    x2 = _parser.CalculateReal();
                    var ux2 = _parser.Units;
                    if (!Unit.IsConsistent(ux1, ux2))
#if BG
                        throw new MathParserException($"Несъвместими мерни единици за {_items[0].Input} = \"{Unit.GetText(ux1)}' : \"{Unit.GetText(ux2)}'.");
#else
                        throw new MathParserException($"Inconsistent units for {_items[0].Input} = \"{Unit.GetText(ux1)}' : \"{Unit.GetText(ux2)}'.");
#endif
                    if (ux2 is not null)
                        x2 *= ux2.ConvertTo(ux1);
                }
                x.SetValue(x1, ux1);
                if (_type == SolverTypes.Root && _items[4].Rpn is not null)
                {
                    _function();
                    var uy1 = _parser.Units;
                    _parser._rpn = _items[4].Rpn;
                    var y1 = _parser.CalculateReal();
                    x.SetNumber(x2);
                    var y2 = _parser.CalculateReal();
                    if (Math.Abs(y2 - y1) > 1e-14)
#if BG
                        throw new MathParserException($"Изразът от дясната страна трябва да е константа: \"{_items[4].Input}\".");
#else
                        throw new MathParserException($"The expression on the right side must be constant: \"{_items[4].Input}\".");
#endif
                    y = y1;
                    var uy2 = _parser.Units;
                    if (!Unit.IsConsistent(uy1, uy2))
#if BG
                        throw new MathParserException($"Несъвместими мерни единици за \"{ _items[0].Input} = {_items[4].Input}\".");
#else
                        throw new MathParserException($"Inconsistent units for \"{_items[0].Input} = {_items[4].Input}\".");
#endif
                    if (uy2 is not null)
                        y *= uy2.ConvertTo(uy1);
                }
                var solver = _parser._solver;
                var variable = solver.Variable;
                var function = solver.Function;
                var solverUnits = solver.Units;
                solver.Variable = x;
                solver.Function = _function;
                solver.Precision = _parser.Precision;
                solver.Units = null;
                var result = new Complex(double.NaN);
                try
                {
                    switch (_type)
                    {
                        case SolverTypes.Find:
                            result = solver.Find(x1, x2);
                            break;
                        case SolverTypes.Root:
                            result = solver.Root(x1, x2, y);
                            break;
                        case SolverTypes.Sup:
                            result = solver.Sup(x1, x2);
                            break;
                        case SolverTypes.Inf:
                            result = solver.Inf(x1, x2);
                            break;
                        case SolverTypes.Area:
                            result = solver.Area(x1, x2);
                            break;
                        case SolverTypes.Repeat:
                            result = solver.Repeat(x1, x2);
                            break;
                        case SolverTypes.Sum:
                            result = solver.Sum(x1, x2);
                            break;
                        case SolverTypes.Product:
                            result = solver.Product(x1, x2);
                            break;
                        case SolverTypes.Slope:
                            result = solver.Slope(x1);
                            break;
                    }
                }
                catch (MathParserException e)
                {
                    if (e.Message.Contains("%F"))
                    {
                        var s = e.Message.Replace("%F", _items[0].Input).Replace("%V", _items[1].Input);
                        throw new MathParserException(s);
                    }
                    throw e;
                }
                _parser._rpn = parserRpn;
                if (_type == SolverTypes.Sup || _type == SolverTypes.Inf)
                {
                    var s = _items[1].Input + (_type == SolverTypes.Sup ? "_sup" : "_inf");
                    _parser.SetVariable(s, x.Value);
                }

                _parser.IsPlotting = isPlotting;
                _parser.Units = parserUnits;
                _parser._targetUnits = targetUnits;
                _parser._isSolver = false;

                if (double.IsNaN(result.Re) && !isPlotting)
#if BG
                    throw new MathParserException($"Няма решение за: {ToString()}.");
#else
                    throw new MathParserException($"No solution for: {ToString()}.");
#endif

                Result = new Value(result, solver.Units);
                solver.Variable = variable;
                solver.Function = function;
                solver.Units = solverUnits;
                return Result;
            }

            internal string ToHtml()
            {
                _stringBuilder.Clear();
                if (_type == SolverTypes.Sum || _type == SolverTypes.Product)
                    _stringBuilder.Append("<span class=\"cond b1\">" + TypeName(_type) + "</span><span class=\"b1\">{</span> ");
                else
                    _stringBuilder.Append("<span class=\"cond\">" + TypeName(_type) + "</span>{");

                _stringBuilder.Append(_items[0].Html);
                if (_type == SolverTypes.Root)
                {
                    if (_items[4].Html is not null)
                        _stringBuilder.Append(" = " + _items[4].Html);
                    else
                        _stringBuilder.Append(" = 0");
                }

                _stringBuilder.Append("; ");
                _stringBuilder.Append(_items[1].Html);
                if (_type == SolverTypes.Repeat || _type == SolverTypes.Sum || _type == SolverTypes.Product)
                {
                    _stringBuilder.Append(" = ");
                    _stringBuilder.Append(_items[2].Html);
                    _stringBuilder.Append("...");
                    _stringBuilder.Append(_items[3].Html);
                    if (_type == SolverTypes.Repeat)
                        _stringBuilder.Append('}');
                    else
                        _stringBuilder.Append(" <span class=\"b1\">}</span>");
                }
                else if (_type == SolverTypes.Slope)
                {
                    _stringBuilder.Append(" = ");
                    _stringBuilder.Append(_items[2].Html);
                    _stringBuilder.Append('}');
                }
                else
                {
                    _stringBuilder.Append(" ∈ [");
                    _stringBuilder.Append(_items[2].Html);
                    _stringBuilder.Append("; ");
                    _stringBuilder.Append(_items[3].Html);
                    _stringBuilder.Append("]}");
                }
                return _stringBuilder.ToString();
            }

            internal string ToXml()
            {
                _stringBuilder.Clear();
                if (_type == SolverTypes.Sum || _type == SolverTypes.Product)
                    _stringBuilder.Append($"<m:nary><m:naryPr><m:chr m:val=\"{ TypeName(_type)}\"/><m:limLoc m:val=\"undOvr\"/><m:subHide m:val=\"1\"/><m:supHide m:val=\"1\" /></m:naryPr><m:sub/><m:sup/><m:e>");
                else
                    _stringBuilder.Append($"<m:r><m:t>{TypeName(_type)}</m:t></m:r>");//<w:rPr><w:color w:val=\"FF00FF\" /></w:rPr>

                _stringBuilder.Append("<m:d><m:dPr><m:begChr m:val = \"{\" /><m:endChr m:val = \"}\" /></m:dPr><m:e>");

                _stringBuilder.Append(_items[0].Xml);
                if (_type == SolverTypes.Root)
                {
                    if (_items[4].Xml is not null)
                        _stringBuilder.Append($"<m:r><m:t>=</m:t></m:r>{_items[4].Xml}");
                    else
                        _stringBuilder.Append("<m:r><m:t>=0</m:t></m:r>");
                }

                _stringBuilder.Append("<m:r><m:t>;</m:t></m:r>");
                _stringBuilder.Append(_items[1].Xml);
                if (_type == SolverTypes.Repeat || _type == SolverTypes.Sum || _type == SolverTypes.Product)
                {
                    _stringBuilder.Append("<m:r><m:t>=</m:t></m:r>");
                    _stringBuilder.Append(_items[2].Xml);
                    _stringBuilder.Append("<m:r><m:t>...</m:t></m:r>");
                    _stringBuilder.Append(_items[3].Xml);
                }
                else if (_type == SolverTypes.Slope)
                {
                    _stringBuilder.Append("<m:r><m:t>=</m:t></m:r>");
                    _stringBuilder.Append(_items[2].Xml);
                }
                else
                {
                    _stringBuilder.Append("<m:r><m:t>∈</m:t></m:r><m:d><m:dPr><m:begChr m:val = \"[\" /><m:endChr m:val = \"]\" /></m:dPr><m:e>");
                    _stringBuilder.Append(_items[2].Xml);
                    _stringBuilder.Append("<m:r><m:t>;</m:t></m:r>");
                    _stringBuilder.Append(_items[3].Xml);
                    _stringBuilder.Append("</m:e></m:d>");
                }
                _stringBuilder.Append("</m:e></m:d>");
                if (_type == SolverTypes.Sum || _type == SolverTypes.Product)
                    _stringBuilder.Append("</m:e></m:nary>");

                return _stringBuilder.ToString();
            }

            public override string ToString()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(TypeName(_type));
                _stringBuilder.Append('{');
                _stringBuilder.Append(_items[0].Input);
                if (_type == SolverTypes.Root)
                {
                    if (_items[4].Input is not null)
                        _stringBuilder.Append(" = " + _items[4].Input);
                    else
                        _stringBuilder.Append(" = 0");
                }
                _stringBuilder.Append("; ");
                _stringBuilder.Append(_items[1].Input);
                if (_type == SolverTypes.Repeat || _type == SolverTypes.Sum || _type == SolverTypes.Product)
                {
                    _stringBuilder.Append(" = ");
                    _stringBuilder.Append(_items[2].Input);
                    _stringBuilder.Append("...");
                    _stringBuilder.Append(_items[3].Input);
                    _stringBuilder.Append('}');
                }
                else if (_type == SolverTypes.Slope)
                {
                    _stringBuilder.Append(" = ");
                    _stringBuilder.Append(_items[2].Input);
                    _stringBuilder.Append('}');
                }
                else
                {
                    _stringBuilder.Append(" ∈ [");
                    _stringBuilder.Append(_items[2].Input);
                    _stringBuilder.Append("; ");
                    _stringBuilder.Append(_items[3].Input);
                    _stringBuilder.Append("]}");
                }
                return _stringBuilder.ToString();
            }

            private struct SolverItem
            {
                internal string Input;
                internal string Html;
                internal string Xml;
                internal Token[] Rpn;
            }
        }
    }
}