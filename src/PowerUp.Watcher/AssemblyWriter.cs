﻿using PowerUp.Core.Compilation;
using PowerUp.Core.Console;
using PowerUp.Core.Decompilation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PowerUp.Core.Console.XConsole;

namespace PowerUp.Watcher
{
    //
    // This class writes out common formatting
    // that all compilers tend to use for display,
    // to stay consistent, everything else is compiler specific
    // and needs special handling each time.
    //
    public class AssemblyWriter
    {
        //
        // This array contains all of the jump instructions for X86 ISA
        //
        public static string[] JumpInstructions = new string[]
        {
            "jae",
            "jb",
            "jecxz",
            "je",
            "jz",
            "jge",
            "jg",
            "ja",
            "jle",
            "jbe",
            "jl",
            "js",
            "jne",
            "jnz",
            "jno",
            "jo",
            "jnp",
            "jpo",
            "jns",
            "jp",
            "jpe",
            "jmp"
        };
        public int InstructionPad { get; set; } = 6;
        public int AddressPad { get; set; } = 4;
        public int AddressCutBy { get; set; } = 0;
        public int DocumentationOffset { get; set; } = 40;

        public void AppendHelp(StringBuilder helpBuilder)
        {
            var sep = new string(XConsole.ConsoleBorderStyle.TopBottom, 20);
            helpBuilder.AppendLine("# Help:");
            foreach (var cmd in WatcherUtils.upCommands)
            {
                helpBuilder.AppendLine($"#   //{cmd.Name}");
                helpBuilder.AppendLine($"#   {XConsole.ConsoleBorderStyle.BottomLeft}> {cmd.Description}");
                if (cmd.Args.Length > 0)
                {
                    helpBuilder.AppendLine("#   Optional Arguments:");
                    foreach (var arg in cmd.Args)
                        helpBuilder.AppendLine($"#     {arg} = X");
                }
                helpBuilder.AppendLine($"#   " + sep);
                helpBuilder.AppendLine();
            }
        }

        public void AppendInstructionAddress(StringBuilder lineBuilder, AssemblyInstruction inst, bool zeroPad = true)
        {
            //
            // Do nothing with code.
            //
            if (inst.IsCode) return;
            lineBuilder.Append($"{FormatAsAddress(inst.Address, AddressCutBy, AddressPad, zeroPad)}: ");
        }

        private string FormatAsAddress(ulong addr, int cutBy, int padBy, bool zeroPad)
        {
            var address = addr.ToString("X");
            string hexPad = null;
            if (zeroPad)
            {
                var hexSize = AddressPad;
                var padLen = hexSize - address.Length;
                if (padLen < 0) padLen = 0;
                hexPad = new string('0', padLen);
            }

            //
            // The option to shorten addresses was selected but we cannot trim them to len < 0
            // so leave them as is.
            //
            if (address.Length < cutBy)
                return $"{hexPad}{address}";
            else if (cutBy > 0)
                return $"{hexPad}{address.Substring(cutBy)}";
            else
                return $"{hexPad}{address}";
        }

        public void AppendInstructionName(StringBuilder lineBuilder, AssemblyInstruction inst)
        {
            var offset = InstructionPad - inst.Instruction.Length;
            if (offset < 0) offset = 0;
            lineBuilder.Append($"{(inst.IsCode ? "# " : "")}{inst.Instruction} " + new string(' ', offset));
        }
        public void AppendMethodSignature(StringBuilder methodBuilder, DecompiledMethod method)
        {
            methodBuilder.AppendLine($"# Instruction Count: {method.Instructions.Count}; Code Size: {method.CodeSize}");
            methodBuilder.Append($"{(method.TypeName == null ? "" : method.TypeName + "+")}{method.Return} {method.Name}(");

            for (int i = 0; i < method.Arguments.Length; i++)
            {
                methodBuilder.Append($"{method.Arguments[i]}");

                if (i != method.Arguments.Length - 1)
                {
                    methodBuilder.Append(", ");
                }
            }

            methodBuilder.AppendLine("):");
        }

        public void AppendArgument(StringBuilder lineBuilder, DecompiledMethod method, AssemblyInstruction instruction, InstructionArg arg, bool isLast)
        {
            //
            // Check if the instruction was a jump, since jumps need a very special handling,
            // that is slightly different per compiler (but we managed to come up with a sensible default)
            //
            if (instruction.jumpDirection != JumpDirection.None)
            {
                //
                // Try to separate the value from the address or anything else that might be there.
                //
                var addressOrAnyInArg = arg.Value.LastIndexOf(' ');
                var value = arg.Value;
                if (addressOrAnyInArg != -1)
                {
                    value = arg.Value.Substring(0, addressOrAnyInArg);
                }

                //
                // The argument is a jump and since we support many flavors of jumps (from many languages, and compilers),
                // the argument value will most likley be eiter a lablel (LB0001) or reference to a call (THROW_HELPER).
                // The correct address will be stored in the RefAddress.
                //
                // For compilers like Rust (and LLVM) there will be no RefAdress but the label will fill the same function.
                //
                lineBuilder.Append($"{value.Trim()}");
                if (instruction.RefAddress > 0)
                {
                    lineBuilder.Append($" {FormatAsAddress(instruction.RefAddress, AddressCutBy, AddressPad, zeroPad: true)}");
                }

                //
                // Render jump direction guides.
                //
                if (instruction.jumpDirection == JumpDirection.Out)
                    lineBuilder.Append($" ↷");
                else if (instruction.jumpDirection == JumpDirection.Up)
                    lineBuilder.Append($" ⇡");
                else if (instruction.jumpDirection == JumpDirection.Down)
                    lineBuilder.Append($" ⇣");
            }
            else
            {
                //
                // The instruction wasn't a jump so this will be a standard value
                // meaning that it will be an const, register, operator, or array access
                //
                var value = arg.Value.Trim();
                var code = string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    if (c == ']' || c == '[' || c == '+' || c == '-' || c == '*')
                    {
                        if (string.IsNullOrEmpty(code) == false)
                        {
                            lineBuilder.Append($"{code}");
                            code = string.Empty;
                        }

                        lineBuilder.Append($"{c}");
                    }
                    else
                    {
                        code += c;
                    }
                }
                if (string.IsNullOrEmpty(code) == false)
                {
                    lineBuilder.Append($"{code}");
                }
            }

            if (isLast == false)
            {
                lineBuilder.Append($", ");
            }
        }

        public void AppendMessages(StringBuilder methodBuilder, DecompiledMethod method)
        {
            //
            // Print messages.
            //
            if (method.Messages != null && method.Messages.Count > 0)
            {
                methodBuilder.AppendLine(
                    Environment.NewLine +
                    string.Join(Environment.NewLine, method.Messages));
            }
        }
        public void AppendGuides(StringBuilder methodBuilder, AssemblyInstruction inst, (int jumpSize, int nestingLevel) sizeAndNesting)
        {
            int wsCount = 0;
            bool usedGuides = false;
            for (int i = 0; i <= sizeAndNesting.nestingLevel; i++)
            {
                var block = inst.GuideBlocks[i];

                //
                // Guide not found, append whitespace.
                // @TODO: Most of this stuff is done when we're populating guides
                // This loop should be as simple as possible.
                //
                if (block == '\0') wsCount++;
                else
                {
                    if (wsCount > 0) methodBuilder.Append(new String(' ', wsCount));
                    wsCount = 0;
                    methodBuilder.Append((char)block);
                    usedGuides = true;
                }
            }

            if (sizeAndNesting.nestingLevel > 0 && usedGuides == false)
                methodBuilder.Append(' ', sizeAndNesting.nestingLevel);
        }
        public void AppendX86Documentation(StringBuilder lineBuilder, DecompiledMethod method, AssemblyInstruction instruction)
        {
            try
            {
                int lineOffset = DocumentationOffset;
                if (lineBuilder.Length < lineOffset)
                {
                    lineBuilder.Append(' ', lineOffset - lineBuilder.Length);
                }

                switch (instruction.Instruction)
                {
                    case "mov":   DocumentMOV(lineBuilder, instruction); break;
                    case "movsxd":DocumentMOVSXD(lineBuilder, instruction); break;
                    case "movzx": DocumentMOVZX(lineBuilder, instruction); break;
                    case "shl":   DocumentSHL(lineBuilder, instruction); break;
                    case "shr":   DocumentSHR(lineBuilder, instruction); break;
                    case "lea":   DocumentLEA(lineBuilder, instruction); break;
                    case "inc":   DocumentINC(lineBuilder, instruction); break;
                    case "dec":   DocumentDEC(lineBuilder, instruction); break;
                    case "call":  DocumentCALL(lineBuilder, instruction); break;
                    case "push":  DocumentPUSH(lineBuilder, instruction); break;
                    case "pop":   DocumentPOP(lineBuilder, instruction); break;
                    case "add":   DocumentADD(lineBuilder, instruction); break;
                    case "sub":   DocumentSUB(lineBuilder, instruction); break;
                    case "xor":   DocumentXOR(lineBuilder, instruction); break;
                    case "ret":   DocumentRET(lineBuilder, instruction); break;
                    case "cmp":   DocumentCMP(lineBuilder, instruction, method); break;
                    case "test":  DocumentTEST(lineBuilder, instruction, method); break;
                    default:
                        {
                            if (instruction.Instruction.StartsWith(".") && instruction.Instruction.EndsWith(":"))
                            {
                                lineBuilder.Append($" # jump label");
                            }
                            else if (JumpInstructions.Contains(instruction.Instruction))
                            {
                                var prev = method.Instructions[instruction.OrdinalIndex - 1];
                                var guide = prev.Instruction == "cmp" ? XConsole.ConsoleBorderStyle.BottomLeft.ToString() + "> " : "";
                                var lhs = instruction.RefAddress.ToString("X");
                                lineBuilder.Append($" # {guide}goto {lhs}");
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                //
                // Ignore this error, we don't want to crash the output if documentaton
                // blows up.
                //
                // Log it to the console and move on.
                //
                XConsole.WriteLine($"'Documentation Generation Failed with message: {ex.Message}'");
            }
        }

        private void DocumentMOV(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            lineBuilder.Append($" # {lhs} = {rhs}");
        }

        private void DocumentTEST(StringBuilder lineBuilder, AssemblyInstruction instruction, DecompiledMethod method)
        {
            if (instruction.OrdinalIndex + 1 < method.Instructions.Count)
            {
                string @operator = "NA";
                var inst = instruction;
                var next = method.Instructions[instruction.OrdinalIndex + 1];
                @operator = SetOperatorForASMDocs(next);
                lineBuilder.Append($" # if({inst.Arguments[0].Value.Trim()} & {inst.Arguments[1].Value} {@operator} 0)");
            }
        }

        private void DocumentMOVZX(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = instruction.Arguments[0].Value.Trim();
            var rhs = instruction.Arguments[1].Value.Trim();

            if (lhs.StartsWith("e")) { lhs = "(32bit)" + lhs; }
            if (rhs.StartsWith("r"))
            {
                if (rhs.EndsWith("d")) { rhs = "(32bit)" + rhs; }
                else if (rhs.EndsWith("w")) { rhs = "(16bit)" + rhs; }
                else if (rhs.EndsWith("b")) { rhs = "(8bit)" + rhs; }
            }
            else if (rhs.Length == 2) { rhs = "(16bit)" + rhs; }


            lineBuilder.Append($" # {lhs} = {rhs} (zero extend)");
        }

        private void DocumentMOVSXD(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs.StartsWith("r")) { lhs = "(64bit)" + lhs; }
            if (rhs.StartsWith("e")) { rhs = "(32bit)" + rhs; }
            else if (rhs.StartsWith("r"))
            {
                if (rhs.EndsWith("d")) { rhs = "(32bit)" + rhs; }
                else if (rhs.EndsWith("w")) { rhs = "(16bit)" + rhs; }
                else if (rhs.EndsWith("b")) { rhs = "(8bit)" + rhs; }
            }

            lineBuilder.Append($" # {lhs} = {rhs} (sign extend)");
        }

        private void DocumentSHR(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs.StartsWith("r")) { lhs = "(64bit)" + lhs; }
            if (rhs.StartsWith("e")) { rhs = "(32bit)" + rhs; }

            lineBuilder.Append($" # {lhs} >> {rhs}");
        }

        private void DocumentSHL(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs.StartsWith("r")) { lhs = "(64bit)" + lhs; }
            if (rhs.StartsWith("e")) { rhs = "(32bit)" + rhs; }

            lineBuilder.Append($" # {lhs} << {rhs}");
        }

        private void DocumentLEA(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = instruction.Arguments[1].Value.Trim();

            if (rhs.StartsWith("[")) { rhs = rhs.Replace("[", "").Replace("]", ""); }
            lineBuilder.Append($" # {lhs} = {rhs}");
        }

        private void DocumentINC(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);

            lineBuilder.Append($" # {lhs}++");
        }

        private void DocumentDEC(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);

            lineBuilder.Append($" # {lhs}--");
        }

        private void DocumentCALL(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);

            if (lhs.StartsWith("CORINFO") && lhs.EndsWith("FAIL"))
            {
                lineBuilder.Append($" # throw");
            }
        }
        private void DocumentPUSH(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);

            lineBuilder.Append($" # stack.push({lhs})");
        }

        private void DocumentPOP(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);

            lineBuilder.Append($" # {lhs} = stack.pop()");
        }

        private void DocumentADD(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs == "rsp")
            {
                lineBuilder.Append($" # stack.pop_times({int.Parse(rhs) / 8})");
            }
            else
            {
                lineBuilder.Append($" # {lhs} += {rhs}");
            }
        }

        private void DocumentSUB(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs == "rsp")
            {
                lineBuilder.Append($" # stack.push_times({int.Parse(rhs) / 8})");
            }
            else
            {
                lineBuilder.Append($" # {lhs} -= {rhs}");
            }
        }

        private void DocumentXOR(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            var lhs = FormatArgument(instruction.Arguments[0].Value);
            var rhs = FormatArgument(instruction.Arguments[1].Value);

            if (lhs == rhs)
                lineBuilder.Append($" # {lhs} = 0");
            else
                lineBuilder.Append($" # {lhs} ^= {rhs}");
        }

        private void DocumentRET(StringBuilder lineBuilder, AssemblyInstruction instruction)
        {
            lineBuilder.Append($" # return;");
        }

        private void DocumentCMP(StringBuilder lineBuilder, AssemblyInstruction instruction, DecompiledMethod method)
        {
            if (instruction.OrdinalIndex + 1 < method.Instructions.Count)
            {
                string @operator = "NA";
                var inst = instruction;
                var next = method.Instructions[instruction.OrdinalIndex + 1];

                var lhs = FormatArgument(instruction.Arguments[0].Value);
                var rhs = FormatArgument(instruction.Arguments[1].Value);

                @operator = SetOperatorForASMDocs(next);
                lineBuilder.Append($" # if({lhs} {@operator} {rhs})");
            }
        }

        private string FormatArgument(string arg)
        {
            var lhs = arg.Trim();
            var indexerTypeValue = "";

            bool isMemory = false;
            if (lhs.StartsWith("dword ptr"))
            {
                isMemory = true;
                indexerTypeValue = "(32bit)Memory";
            }
            else if (lhs.StartsWith("word ptr"))
            {
                isMemory = true;
                indexerTypeValue = "(16bit)Memory";
            }
            else if (lhs.StartsWith("byte ptr"))
            {
                isMemory = true;
                indexerTypeValue = "(8bit)Memory";
            }
            else if (lhs.StartsWith("["))
            {
                isMemory = true;
                indexerTypeValue = "Memory";
            }

            //
            // Parse each individual term in the indexer
            //
            if (isMemory)
            {
                StringBuilder indexerValueBuilder = new StringBuilder();
                var startFrom = lhs.IndexOf('[');
                var endTo     = lhs.IndexOf("]");
                var value = "";
                for(int i = startFrom + 1; i < endTo + 1; i++)
                {
                    var c = lhs[i];
                    //
                    // Operator
                    //
                    if(c == '+' || c == '-' || c == '*' || c == ']')
                    {
                        var argValue = FormatArgument(value);
                        value = "";

                        indexerValueBuilder.Append(argValue);
                        if (c != ']')
                            indexerValueBuilder.Append(c);
                    }
                    else
                    {
                        value += c;
                    }
                }

                lhs = $"{indexerTypeValue}[{indexerValueBuilder.ToString()}]";
            }

            if (IsHex(lhs))
                lhs = HexToDecimal(lhs);

            return lhs;
        }

        private bool IsHex(string value)
        {
            if (value != null)
            {
                return char.IsDigit(value[0]) && value[value.Length - 1] == 'h';
            }
            return false;
        }
        private string HexToDecimal(string value)
        {
            var hexEssence = value.Substring(0, value.Length - 1);
            var decValue = Convert.ToInt64(hexEssence, 16);
            return decValue.ToString();
        }
        private string SetOperatorForASMDocs(AssemblyInstruction instruction)
        {
            return instruction.Instruction switch
            {
                "je" => "==",
                "jne" => "!=",
                "jl" => "<",
                "jg" => ">",
                "jle" => "<=",
                "jge" => ">=",
                //
                // Unsigned
                //
                "jae" => ">=",
                "jbe" => "<=",
                "ja" => ">",
                "jb" => "<",

                _ => "NA"
            };
        }
    }
}
