namespace Orblit.Editor.App.Tests;

public sealed class ScriptCheckerTests
{
    const string Project = "/games/field";

    // As dotnet build prints them: each problem, then the same lines again under Build FAILED.
    const string Output = """
        /games/field/Broken.cs(1,31): error CS1525: Invalid expression term ';' [/games/field/Field.csproj]
        /games/field/ai/Slime.cs(12,5): warning CS0168: The variable 'speed' is declared but never used [/games/field/Field.csproj]

        Build FAILED.

        /games/field/Broken.cs(1,31): error CS1525: Invalid expression term ';' [/games/field/Field.csproj]
        /games/field/ai/Slime.cs(12,5): warning CS0168: The variable 'speed' is declared but never used [/games/field/Field.csproj]
            1 Warning(s)
            1 Error(s)
        """;

    [Fact]
    public void Each_problem_is_read_once_with_its_place_and_a_project_file_is_named_by_its_res_path()
    {
        var problems = ScriptChecker.Read(Output, Project, 1);

        Assert.Equal(
            [
                new ScriptProblem("res://Broken.cs", 1, 31, true, "CS1525: Invalid expression term ';'"),
                new ScriptProblem("res://ai/Slime.cs", 12, 5, false, "CS0168: The variable 'speed' is declared but never used"),
            ],
            problems);
    }

    [Fact]
    public void A_problem_with_no_place_keeps_the_name_it_was_given()
    {
        var problems = ScriptChecker.Read("CSC : error CS2001: Source file 'gone.cs' could not be found. [/games/field/Field.csproj]", Project, 1);

        Assert.Equal([new ScriptProblem("CSC", 0, 0, true, "CS2001: Source file 'gone.cs' could not be found.")], problems);
    }

    [Fact]
    public void A_failed_build_with_no_error_to_show_still_says_it_failed()
    {
        Assert.Equal("The build failed with code 1. Output from dotnet build shows why.", Assert.Single(ScriptChecker.Read("MSBUILD : something odd", Project, 1)).Text);
        Assert.Empty(ScriptChecker.Read("Build succeeded.", Project, 0));
    }

    [Fact]
    public void A_folder_with_no_C_sharp_project_cannot_be_checked()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var error = Assert.Throws<AggregateException>(() => ScriptChecker.Check(folder).Wait());
            Assert.Equal("The project has no C# project file to check.", Assert.IsType<IOException>(error.InnerException).Message);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }
}
