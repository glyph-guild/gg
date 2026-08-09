namespace Gg.Console.Tests;

public class EditorSessionTests
{
    [Test]
    public async Task EditSpawnsARealChildProcessAndReturnsItsEdit()
    {
        // A real external "editor": a shell script that appends a line to the
        // file it is handed — a separate OS process, exactly like $EDITOR.
        var editorScript = Path.Combine(Path.GetTempPath(), $"gg-fake-editor-{Guid.NewGuid():N}.sh");
        File.WriteAllText(editorScript, "#!/bin/sh\nprintf 'edited by pid %s\\n' $$ >> \"$1\"\n");
        try
        {
            var edited = new EditorSession($"/bin/sh {editorScript}").Edit("original text\n");

            await Assert.That(edited).StartsWith("original text\n");
            await Assert.That(edited).Contains("edited by pid ");
        }
        finally
        {
            File.Delete(editorScript);
        }
    }
}
