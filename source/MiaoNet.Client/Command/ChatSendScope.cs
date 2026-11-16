namespace Celeste.Mod.MiaoNet;

/// <summary>Where a command sends the free text it takes.</summary>
public enum ChatSendScope : byte
{
    /// <summary>The command does not send chat.</summary>
    None,

    /// <summary>Sends to whichever chat channel is currently selected.</summary>
    Current,

    Global,
    Channel,
    Map,
}
