namespace CustomToneMapping.Baker
{
    public interface ILutConfig
    {
        uint ConfigHash { get; }
        int LutSize { get; }
    }
}