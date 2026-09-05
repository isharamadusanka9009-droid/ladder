using System.Collections.Generic;
using LadderToArduino.Models;

namespace LadderToArduino.Services
{
    // Wraps a Simulator with observable items the Simulate tab can bind to directly.
    // Toggling a SimBoolItem/SimIntItem for an input writes straight into the Simulator's
    // arrays; after each Tick() the output-side items are refreshed from the Simulator.
    public class SimulationViewModel
    {
        public Simulator Sim { get; }

        public List<SimBoolItem> SimInputs { get; } = new List<SimBoolItem>();
        public List<SimIntItem> SimAnalogInputs { get; } = new List<SimIntItem>();
        public List<SimBoolItem> SimOutputsQ { get; } = new List<SimBoolItem>();
        public List<SimBoolItem> SimMemory { get; } = new List<SimBoolItem>(); // M then T then C, in that order
        public List<SimIntItem> SimAnalogOutputs { get; } = new List<SimIntItem>();

        public SimulationViewModel(LadderProgram program)
        {
            Sim = new Simulator(program);

            for (int i = 0; i < Sim.I.Length; i++)
            {
                var item = new SimBoolItem { Label = "I" + i };
                int idx = i;
                item.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SimBoolItem.Value)) Sim.I[idx] = item.Value; };
                SimInputs.Add(item);
            }

            for (int i = 0; i < Sim.AI.Length; i++)
            {
                var item = new SimIntItem { Address = "AI" + i, Label = "AI" + i };
                int idx = i;
                item.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SimIntItem.Value)) Sim.AI[idx] = item.Value; };
                SimAnalogInputs.Add(item);
            }

            for (int i = 0; i < Sim.Q.Length; i++) SimOutputsQ.Add(new SimBoolItem { Label = "Q" + i });
            for (int i = 0; i < Sim.M.Length; i++) SimMemory.Add(new SimBoolItem { Label = "M" + i });
            for (int i = 0; i < Sim.T.Length; i++) SimMemory.Add(new SimBoolItem { Label = "T" + i });
            for (int i = 0; i < Sim.C.Length; i++) SimMemory.Add(new SimBoolItem { Label = "C" + i });
            for (int i = 0; i < Sim.AO.Length; i++) SimAnalogOutputs.Add(new SimIntItem { Address = "AO" + i, Label = "AO" + i + ": 0" });
        }

        public void Tick()
        {
            Sim.Tick();

            for (int i = 0; i < SimOutputsQ.Count; i++) SimOutputsQ[i].Value = Sim.Q[i];

            int mi = 0;
            for (int i = 0; i < Sim.M.Length; i++) SimMemory[mi++].Value = Sim.M[i];
            for (int i = 0; i < Sim.T.Length; i++) SimMemory[mi++].Value = Sim.T[i].Q;
            for (int i = 0; i < Sim.C.Length; i++) SimMemory[mi++].Value = Sim.C[i].Q;

            for (int i = 0; i < SimAnalogOutputs.Count; i++)
            {
                SimAnalogOutputs[i].Value = Sim.AO[i];
                SimAnalogOutputs[i].Label = $"AO{i}: {Sim.AO[i]}";
            }
        }
    }
}
