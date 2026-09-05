namespace LadderToArduino.Models
{
    public enum AddressKind
    {
        Input,          // I  - physical digital input pin
        Output,         // Q  - physical digital output pin
        Memory,         // M  - internal auxiliary relay (bool, no physical pin)
        Timer,          // T  - timer done-bit (contact) / timer target (coil)
        Counter,        // C  - counter done-bit (contact) / counter target (coil)
        AnalogInput,    // AI - analogRead() pin, used with a Comparator on a contact
        PWMOutput       // AO - analogWrite() PWM pin, used as an AnalogOutput coil
    }

    public enum ContactMode
    {
        NormallyOpen,
        NormallyClosed
    }

    // Only used when a Contact's Kind == AnalogInput
    public enum ComparatorOp
    {
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Equal
    }

    public enum CoilType
    {
        Output,
        Set,
        Reset,
        TimerOnDelay,
        TimerOffDelay,
        CounterUp,
        CounterDown,
        AnalogOutput    // writes a PWM value to a PWMOutput address while the rung is true
    }

    // Where an AnalogOutput coil gets its PWM value from
    public enum AnalogSource
    {
        Constant,             // a fixed 0-255 value typed in
        CopyFromAnalogInput   // pass through analogRead() from an AI address (auto-scaled 0-1023 -> 0-255)
    }
}
