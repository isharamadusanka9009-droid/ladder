using System;
using System.Linq;
using LadderToArduino.Models;

namespace LadderToArduino.Services
{
    public class TimerSim { public bool Running; public long Start; public long Preset; public bool Q; public bool OffRunning; }
    public class CounterSim { public long Count; public long Preset; public bool LastIn; public bool Q; }

    // Mirrors CodeGenerator's semantics exactly, but runs in memory in C# so the app can simulate
    // a ladder program without a physical Arduino attached. Toggle I[]/AI[] from the UI, call Tick()
    // on a timer, and read Q/M/T/C/AO back for display.
    public class Simulator
    {
        public bool[] I = new bool[1];
        public bool[] Q = new bool[1];
        public bool[] M = new bool[1];
        public int[] AI = new int[1];
        public int[] AO = new int[1];
        public TimerSim[] T = new TimerSim[1];
        public CounterSim[] C = new CounterSim[1];

        public int TickIntervalMs = 100; // virtual ms advanced per Tick()
        private long _virtualMillis;
        private LadderProgram _program;

        public long VirtualMillis => _virtualMillis;

        public Simulator(LadderProgram program) => Load(program);

        public void Load(LadderProgram program)
        {
            _program = program;
            I = new bool[Math.Max(CodeGenerator.SizeFor(program, AddressKind.Input), 1)];
            Q = new bool[Math.Max(CodeGenerator.SizeFor(program, AddressKind.Output), 1)];
            M = new bool[Math.Max(CodeGenerator.SizeFor(program, AddressKind.Memory), 1)];
            AI = new int[Math.Max(CodeGenerator.SizeFor(program, AddressKind.AnalogInput), 1)];
            AO = new int[Math.Max(CodeGenerator.SizeFor(program, AddressKind.PWMOutput), 1)];
            T = Enumerable.Range(0, Math.Max(CodeGenerator.SizeFor(program, AddressKind.Timer), 1)).Select(_ => new TimerSim()).ToArray();
            C = Enumerable.Range(0, Math.Max(CodeGenerator.SizeFor(program, AddressKind.Counter), 1)).Select(_ => new CounterSim()).ToArray();

            foreach (var rung in program.Rungs)
                foreach (var o in rung.Outputs)
                {
                    if (o.CoilType == CoilType.TimerOnDelay || o.CoilType == CoilType.TimerOffDelay)
                        T[o.OutputIndex].Preset = o.Preset;
                    if (o.CoilType == CoilType.CounterUp || o.CoilType == CoilType.CounterDown)
                        C[o.OutputIndex].Preset = o.Preset;
                }

            _virtualMillis = 0;
        }

        public void Reset() => Load(_program);

        public void Tick()
        {
            _virtualMillis += TickIntervalMs;
            foreach (var rung in _program.Rungs)
            {
                bool result = EvaluateRung(rung);
                foreach (var o in rung.Outputs)
                    ApplyCoil(o, result);
            }
        }

        private bool EvaluateRung(Rung rung) =>
            rung.Branches.Any(b => b.Contacts.Count > 0 && b.Contacts.All(EvaluateContact));

        private bool EvaluateContact(Contact c)
        {
            if (c.Kind == AddressKind.AnalogInput)
            {
                int val = AI[c.Index];
                bool cmp;
                switch (c.Operator)
                {
                    case ComparatorOp.GreaterThan: cmp = val > c.Threshold; break;
                    case ComparatorOp.GreaterOrEqual: cmp = val >= c.Threshold; break;
                    case ComparatorOp.LessThan: cmp = val < c.Threshold; break;
                    case ComparatorOp.LessOrEqual: cmp = val <= c.Threshold; break;
                    case ComparatorOp.Equal: cmp = val == c.Threshold; break;
                    default: cmp = false; break;
                }
                return c.Mode == ContactMode.NormallyClosed ? !cmp : cmp;
            }

            bool raw = RawValue(c.Kind, c.Index);
            return c.Mode == ContactMode.NormallyClosed ? !raw : raw;
        }

        private bool RawValue(AddressKind kind, int idx)
        {
            switch (kind)
            {
                case AddressKind.Input: return I[idx];
                case AddressKind.Output: return Q[idx];
                case AddressKind.Memory: return M[idx];
                case AddressKind.Timer: return T[idx].Q;
                case AddressKind.Counter: return C[idx].Q;
                default: return false;
            }
        }

        private void SetTarget(AddressKind kind, int idx, bool val)
        {
            if (kind == AddressKind.Memory) M[idx] = val;
            else Q[idx] = val; // Output, or anything else, targets Q
        }

        private void ApplyCoil(CoilOutput o, bool result)
        {
            int idx = o.OutputIndex;
            bool hasReset = o.ResetIndex >= 0;
            bool resetActive = hasReset && RawValue(o.ResetKind, o.ResetIndex);

            switch (o.CoilType)
            {
                case CoilType.Output:
                    SetTarget(o.OutputKind, idx, result);
                    break;

                case CoilType.Set:
                    if (result) SetTarget(o.OutputKind, idx, true);
                    break;

                case CoilType.Reset:
                    if (result) SetTarget(o.OutputKind, idx, false);
                    break;

                case CoilType.TimerOnDelay:
                    if (result)
                    {
                        if (!T[idx].Running) { T[idx].Running = true; T[idx].Start = _virtualMillis; }
                        T[idx].Q = (_virtualMillis - T[idx].Start) >= T[idx].Preset;
                    }
                    else { T[idx].Running = false; T[idx].Q = false; }
                    if (resetActive) { T[idx].Running = false; T[idx].Q = false; }
                    break;

                case CoilType.TimerOffDelay:
                    if (result) { T[idx].Q = true; T[idx].OffRunning = false; }
                    else
                    {
                        if (!T[idx].OffRunning) { T[idx].OffRunning = true; T[idx].Start = _virtualMillis; }
                        if ((_virtualMillis - T[idx].Start) >= T[idx].Preset) T[idx].Q = false;
                    }
                    if (resetActive) { T[idx].Q = false; T[idx].OffRunning = false; }
                    break;

                case CoilType.CounterUp:
                    if (result && !C[idx].LastIn) C[idx].Count++;
                    C[idx].LastIn = result;
                    C[idx].Q = C[idx].Count >= C[idx].Preset;
                    if (resetActive) { C[idx].Count = 0; C[idx].Q = false; }
                    break;

                case CoilType.CounterDown:
                    if (result && !C[idx].LastIn) C[idx].Count--;
                    C[idx].LastIn = result;
                    C[idx].Q = C[idx].Count <= 0;
                    if (resetActive) { C[idx].Count = C[idx].Preset; C[idx].Q = false; }
                    break;

                case CoilType.AnalogOutput:
                    int value = o.AnalogSource == AnalogSource.Constant
                        ? o.AnalogConstant
                        : Map(AI[o.AnalogSourceIndex], 0, 1023, 0, 255);
                    AO[idx] = result ? value : 0;
                    break;
            }
        }

        private static int Map(int x, int inMin, int inMax, int outMin, int outMax) =>
            (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    }
}
