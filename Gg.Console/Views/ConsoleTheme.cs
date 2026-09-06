using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Gg.Console.Views;

/// <summary>
/// Which theme the console runs on, what ground it draws on, and what "muted"
/// means on that ground.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in <see cref="ConsoleScreen"/>, because this is the one
/// decision in the view that has a right and a wrong answer.</b> Mixing the
/// colours inside the screen put them where no test can reach them - nothing
/// can construct a <c>ConsoleScreen</c> without a terminal - and the muted
/// panes shipped as dark text on a light block. Everything else in the screen
/// is layout, which a person has to look at anyway.
/// </para>
/// </remarks>
public static class ConsoleTheme
{
    /// <summary>The theme the console asks for.</summary>
    public const string Dark = "Dark";

    /// <summary>The scheme every pane starts from.</summary>
    private const string Base = "Base";

    /// <summary>
    /// The ground the console draws on, named here rather than inherited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Color.None"/> is what went wrong, and it is not a colour.</b>
    /// Every background in the Dark theme's <c>Base</c> scheme is <c>None</c>,
    /// meaning "leave whatever the terminal is showing" - so the foreground was
    /// the only half this console chose, and it was chosen against a ground
    /// nobody here could see. A mid grey on a light terminal is dark text on a
    /// grey block, which is what a person got. Naming the ground makes both
    /// halves of every attribute known, and makes "dimmer" a thing that can be
    /// computed instead of guessed.
    /// </para>
    /// <para>
    /// Onyx is the Dark theme's own ground - the colour it already uses behind a
    /// selected row - so the console is not inventing a palette, it is finishing
    /// the one the theme starts.
    /// </para>
    /// </remarks>
    public static Color Ground { get; } = new(StandardColor.Onyx);

    /// <summary>
    /// The configuration the themes come out of, enabled once per process.
    /// </summary>
    private static bool _loaded;

    /// <summary>Whichever theme the library says is in force.</summary>
    /// <remarks>
    /// The library's answer rather than a field of our own, so an
    /// <see cref="Apply"/> that asked for a theme nobody defines reports the one
    /// that is actually drawing.
    /// </remarks>
    public static string Active
    {
        get
        {
#pragma warning disable CS0618 // See Apply: the replacement cannot switch themes in 2.4.17.
            return ThemeManager.Theme;
#pragma warning restore CS0618
        }
    }

    /// <summary>
    /// Put the console on its theme. Safe to call again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sources have to be loaded before the name means anything.</b> Every
    /// theme but Default lives in <c>Terminal.Gui</c>'s own resources and is
    /// absent until they are loaded, so naming one without loading them leaves
    /// the light default in force with no error anywhere. That is why the tests
    /// beside this assert colours and not the string.
    /// </para>
    /// <para>
    /// <b>ONLY LIBRARY RESOURCES.</b> <c>~/.tui/config.json</c> and
    /// <c>./.tui/config.json</c> would let a file this console has never heard of
    /// change what it draws - including a file in whatever repository the person
    /// happens to be standing in.
    /// </para>
    /// <para>
    /// <b>AND ON THE OBSOLETE MANAGER, DELIBERATELY.</b> Obsolete warnings are
    /// errors here and this is the one suppression in the console, because the
    /// replacement does not do this job in Terminal.Gui 2.4.17:
    /// <c>TuiConfigurationBuilder</c> loads the library's <c>Themes</c> array
    /// into its <c>IConfiguration</c> - all seven of them are there - but
    /// <c>MecThemeManager.ThemeNames</c> reports only <c>Default</c> and
    /// <c>SwitchTheme("Dark")</c> returns <see langword="false"/> while still
    /// setting the name, so a console built on it reports the theme it asked for
    /// and draws the one it did not get. Measured, not assumed. Revisit on the
    /// next Terminal.Gui bump.
    /// </para>
    /// </remarks>
    public static void Apply()
    {
#pragma warning disable CS0618
        if (!_loaded)
        {
            ConfigurationManager.Enable(ConfigLocations.LibraryResources);
            _loaded = true;
        }

        if (ThemeManager.Theme != Dark)
        {
            ThemeManager.Theme = Dark;
            ConfigurationManager.Apply();
        }
#pragma warning restore CS0618
    }

    /// <summary>
    /// The theme's scheme, standing on the console's own ground.
    /// </summary>
    /// <remarks>
    /// Only the backgrounds the theme left as <see cref="Color.None"/> are
    /// filled. The roles it does colour - a selected row, a focused field - are
    /// its own answers and are already on a dark ground.
    /// </remarks>
    public static Scheme Grounded()
    {
        var basis = SchemeManager.GetScheme(Base);

        return new Scheme(basis)
        {
            Normal = OnGround(basis.GetAttributeForRole(VisualRole.Normal)),
            HotNormal = OnGround(basis.GetAttributeForRole(VisualRole.HotNormal)),
            Disabled = OnGround(basis.GetAttributeForRole(VisualRole.Disabled)),
            ReadOnly = OnGround(basis.GetAttributeForRole(VisualRole.ReadOnly)),
            Editable = OnGround(basis.GetAttributeForRole(VisualRole.Editable)),
        };
    }

    /// <summary>
    /// Dimmer text, on the ground the rest of the console is already on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Halfway to the ground, rather than a grey somebody liked.</b> A named
    /// colour is right on one theme and wrong on the next, and "muted" has an
    /// arithmetic meaning: partway from the text around it towards the surface
    /// under it. Computing it that way is also what makes it checkable - the
    /// test asserts it lands between the two, which no fixed grey can promise.
    /// </para>
    /// <para>
    /// <b>The background is copied, never chosen.</b> A document pane that names
    /// its own background is a rectangle of a different colour in the middle of
    /// the console whichever colour it names.
    /// </para>
    /// </remarks>
    public static Scheme Muted()
    {
        var grounded = Grounded();
        var normal = grounded.GetAttributeForRole(VisualRole.Normal);

        return new Scheme(grounded)
        {
            Normal = new Terminal.Gui.Drawing.Attribute(
                Halfway(normal.Foreground, normal.Background),
                normal.Background,
                normal.Style | TextStyle.Faint),
        };
    }

    private static Terminal.Gui.Drawing.Attribute OnGround(
        Terminal.Gui.Drawing.Attribute attribute) =>
        attribute.Background == Color.None
            ? new Terminal.Gui.Drawing.Attribute(attribute.Foreground, Ground, attribute.Style)
            : attribute;

    private static Color Halfway(Color from, Color to) => new(
        (byte)((from.R + to.R) / 2),
        (byte)((from.G + to.G) / 2),
        (byte)((from.B + to.B) / 2));
}
