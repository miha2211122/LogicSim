using LogicSim.Game.Core;

namespace LogicSim.Game.Rendering;

public sealed class TutorialCase
{
    public int[] Inputs { get; init; } = Array.Empty<int>();
    public LogicState[] Outputs { get; init; } = Array.Empty<LogicState>();
}

public sealed class TutorialCheck
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string Table { get; init; } = "";
    public int CaseIndex { get; init; } = -1;
}

/// <summary>Уровень обучения: ограничения, подсказка и функциональная проверка схемы.</summary>
public sealed class TutorialLevel
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string Task { get; init; } = "";
    public string Hint { get; init; } = "";
    public int MaxParts { get; init; }
    public bool AllowMacros { get; init; }
    public bool RequireMacro { get; init; }
    public IReadOnlySet<Palette.Tool> AllowedTools { get; init; } = new HashSet<Palette.Tool>();
    public IReadOnlyList<TutorialCase> Cases { get; init; } = Array.Empty<TutorialCase>();

    private static Palette.Tool[] Tools(params Palette.Tool[] tools) => tools;

    private static LogicState B(bool v) => v ? LogicState.One : LogicState.Zero;
    private static LogicState Z => LogicState.HighZ;

    private static List<TutorialCase> Table(int[] inputWidths, Func<int[], LogicState[]> expected)
    {
        var result = new List<TutorialCase>();
        var values = new int[inputWidths.Length];

        void Walk(int index)
        {
            if (index == values.Length)
            {
                result.Add(new TutorialCase
                {
                    Inputs = (int[])values.Clone(),
                    Outputs = expected(values)
                });
                return;
            }
            for (int v = 0; v < (1 << inputWidths[index]); v++)
            {
                values[index] = v;
                Walk(index + 1);
            }
        }

        Walk(0);
        return result;
    }

    public static IReadOnlyList<TutorialLevel> All { get; } = new[]
    {
        new TutorialLevel
        {
            Number = 1,
            Title = "First wire",
            Task = "Pass the IN switch straight to OUT. OUT must repeat A for both switch positions.",
            Hint = "Click IN in the palette, then click empty space to place it. Do the same with OUT. Drag from the IN output pin to the OUT input pin. Click the IN switch to toggle it.",
            MaxParts = 2,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out).ToHashSet(),
            Cases = Table(new[] { 1 }, v => new[] { B(v[0] == 1) })
        },
        new TutorialLevel
        {
            Number = 2,
            Title = "Inverter",
            Task = "Build NOT A using only NAND gates. OUT = 0 when A = 1, and 1 when A = 0.",
            Hint = "A NAND gate with both inputs tied to the same signal acts as NOT. Wire A to BOTH inputs of one NAND, then wire its output to OUT.",
            MaxParts = 4,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1 }, v => new[] { B(v[0] == 0) })
        },
        new TutorialLevel
        {
            Number = 3,
            Title = "AND from NAND",
            Task = "Build OUT = A AND B using NAND gates only.",
            Hint = "AND is a NAND followed by an inverter (another NAND with both inputs tied together). You get 2 IN + 3 NAND + 1 OUT = 6 parts.",
            MaxParts = 9,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { B(v[0] == 1 && v[1] == 1) })
        },
        new TutorialLevel
        {
            Number = 4,
            Title = "OR from NAND",
            Task = "Build OUT = A OR B using NAND gates only.",
            Hint = "Invert each input first (two NANDs as NOT), then feed both inverted signals into a third NAND. Finish with one more NAND as NOT.",
            MaxParts = 10,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { B(v[0] == 1 || v[1] == 1) })
        },
        new TutorialLevel
        {
            Number = 5,
            Title = "XOR",
            Task = "Build OUT = A XOR B using NAND gates only.",
            Hint = "n1 = A NAND B; n2 = A NAND n1; n3 = B NAND n1; OUT = n2 NAND n3. That is four NAND gates.",
            MaxParts = 10,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { B(v[0] != v[1]) })
        },
        new TutorialLevel
        {
            Number = 6,
            Title = "Half adder",
            Task = "Inputs A, B. First OUT is the sum (XOR), second OUT is the carry (AND).",
            Hint = "n1=NAND(A,B); sum=NAND(NAND(A,n1),NAND(B,n1)); carry=NAND(n1,n1). Put sum OUT to the LEFT of carry OUT.",
            MaxParts = 12,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { B(v[0] != v[1]), B(v[0] == 1 && v[1] == 1) })
        },
        new TutorialLevel
        {
            Number = 7,
            Title = "Your first macro",
            Task = "Build XOR again, but this time turn it into a macro block and USE the block. OUT must be A XOR B.",
            Hint = "Build XOR like in level 5. Select everything (Ctrl+A), press MACRO (Ctrl+M), name it XOR2. Then place two new INs and one new OUT: wire INs to the chip inputs and the chip output to OUT. A macro block is required.",
            MaxParts = 10,
            AllowMacros = true,
            RequireMacro = true,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { B(v[0] != v[1]) })
        },
        new TutorialLevel
        {
            Number = 8,
            Title = "Three-state gate",
            Task = "Inputs: D (data), E (enable). OUT must equal D when E = 1 and be Z (floating) when E = 0.",
            Hint = "Open LIBRARY and pick 3-STATE. Wire D to pin D, E to pin E, and its output to OUT. Look at OUT: Z means the line is floating.",
            MaxParts = 4,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.TriState).ToHashSet(),
            Cases = Table(new[] { 1, 1 }, v => new[] { v[1] == 1 ? B(v[0] == 1) : Z })
        },
        new TutorialLevel
        {
            Number = 9,
            Title = "Weak sources",
            Task = "No inputs. Left OUT must be constantly 1, right OUT must be constantly 0.",
            Hint = "Open LIBRARY. PULL UP drives a weak 1, PULL DOWN drives a weak 0. Wire PULL UP to the left OUT and PULL DOWN to the right OUT (OUTs are ordered left to right by X).",
            MaxParts = 4,
            AllowedTools = Tools(Palette.Tool.Out, Palette.Tool.PullUp, Palette.Tool.PullDown).ToHashSet(),
            Cases = Table(Array.Empty<int>(), _ => new[] { LogicState.One, LogicState.Zero })
        },
        new TutorialLevel
        {
            Number = 10,
            Title = "Full adder",
            Task = "Inputs A, B, CIN. OUT #1 = sum, OUT #2 = carry. You may reuse your XOR macro from level 7!",
            Hint = "INs A,B,CIN and OUTs SUM,CARRY are ordered left to right. n1=NAND(A,B); x=NAND(NAND(A,n1),NAND(B,n1)) (or your XOR2 macro); p=NAND(x,CIN); q=NAND(x,p); r=NAND(CIN,p); SUM=NAND(q,r); CARRY=NAND(n1,p).",
            MaxParts = 30,
            AllowMacros = true,
            AllowedTools = Tools(Palette.Tool.In, Palette.Tool.Out, Palette.Tool.Nand).ToHashSet(),
            Cases = Table(new[] { 1, 1, 1 }, v =>
            {
                int sum = v[0] ^ v[1] ^ v[2];
                int carry = (v[0] & v[1]) | (v[2] & (v[0] ^ v[1]));
                return new[] { B(sum == 1), B(carry == 1) };
            })
        }
    };

    public TutorialCheck Validate(Circuit circuit, Layout layout)
    {
        var elements = circuit.Elements;

        if (elements.Count > MaxParts)
            return new TutorialCheck { Message = $"Too many parts: {elements.Count}/{MaxParts}. Remove {elements.Count - MaxParts}." };

        bool hasMacro = elements.Any(e => e is ChipElement);
        if (!AllowMacros && hasMacro)
            return new TutorialCheck { Message = "Macros are not allowed on this level yet." };
        if (RequireMacro && !hasMacro)
            return new TutorialCheck { Message = "This level requires a macro block (Ctrl+M). Build the circuit, select all, press MACRO, then wire the block to new IN/OUT elements." };
        if (elements.Any(e => e is ClockElement or DelayElement))
            return new TutorialCheck { Message = "This level does not allow CLK or DELAY." };

        // Считаем только подключённые элементы; неподключённый мусор не влияет на логику.
        var ins = elements.OfType<InElement>()
            .Where(e => circuit.Wires.Any(w => ReferenceEquals(w.From, e.Out)))
            .OrderBy(e => layout.GetX(e)).ThenBy(e => layout.GetY(e)).ToList();
        var outs = elements.OfType<OutElement>()
            .Where(e => circuit.Wires.Any(w => ReferenceEquals(w.To, e.In)))
            .OrderBy(e => layout.GetX(e)).ThenBy(e => layout.GetY(e)).ToList();
        int expectedIns = Cases.Count == 0 ? 0 : Cases[0].Inputs.Length;
        int expectedOuts = Cases.Count == 0 ? 0 : Cases[0].Outputs.Length;

        if (ins.Count != expectedIns)
            return new TutorialCheck
            {
                Message = $"Connect exactly {expectedIns} IN element(s) with wires (found {ins.Count}). Unwired INs are ignored."
            };
        if (outs.Count != expectedOuts)
            return new TutorialCheck
            {
                Message = $"Connect exactly {expectedOuts} OUT element(s) with wires (found {outs.Count}). OUTs are matched left to right."
            };
        if (ins.Any(e => e.BitWidth != 1) || outs.Any(e => e.BitWidth != 1))
            return new TutorialCheck { Message = "This level uses 1-bit inputs and outputs only." };

        Circuit test;
        Layout testLayout;
        try
        {
            string snapshot = SaveSystem.CaptureSnapshot(circuit, layout);
            test = new Circuit();
            testLayout = new Layout();
            SaveSystem.ApplySnapshot(snapshot, test, testLayout);
        }
        catch (Exception ex)
        {
            return new TutorialCheck { Message = $"Could not verify circuit: {ex.Message}" };
        }

        var testIns = test.Elements.OfType<InElement>()
            .Where(e => test.Wires.Any(w => ReferenceEquals(w.From, e.Out)))
            .OrderBy(e => testLayout.GetX(e)).ThenBy(e => testLayout.GetY(e)).ToList();
        var testOuts = test.Elements.OfType<OutElement>()
            .Where(e => test.Wires.Any(w => ReferenceEquals(w.To, e.In)))
            .OrderBy(e => testLayout.GetX(e)).ThenBy(e => testLayout.GetY(e)).ToList();
        test.Running = false;

        var rows = new List<string>();
        for (int c = 0; c < Cases.Count; c++)
        {
            var cs = Cases[c];
            for (int i = 0; i < expectedIns; i++)
                testIns[i].In[0].SetValue(cs.Inputs[i] == 1 ? LogicState.One : LogicState.Zero);

            test.Step();
            test.Step();

            var got = new LogicState[expectedOuts];
            string? failure = null;
            for (int o = 0; o < expectedOuts; o++)
            {
                got[o] = testOuts[o].InternalOut[0].State;
                if (failure is null && got[o] != cs.Outputs[o])
                    failure = $"OUT #{o + 1} expected {Name(cs.Outputs[o])}, got {Name(got[o])}";
            }

            rows.Add($"{Inputs(cs)} -> got {string.Join(" ", got.Select(Name))} | need {string.Join(" ", cs.Outputs.Select(Name))}");

            if (failure is not null)
                return new TutorialCheck
                {
                    Success = false,
                    CaseIndex = c,
                    Message = $"Row {c + 1}/{Cases.Count}: {failure}. {Inputs(cs)}",
                    Table = string.Join("\n", rows)
                };
        }

        return new TutorialCheck
        {
            Success = true,
            Message = $"Truth table passed: {Cases.Count}/{Cases.Count} rows.",
            Table = string.Join("\n", rows)
        };
    }

    private static string Name(LogicState s) => s switch
    {
        LogicState.One => "1",
        LogicState.Zero => "0",
        LogicState.HighZ => "Z",
        _ => "X"
    };

    private static string Inputs(TutorialCase cs)
        => cs.Inputs.Length == 0 ? "No inputs." : "Inputs: " + string.Join(", ", cs.Inputs.Select((v, i) => $"{(char)('A' + i)}={v}"));
}
