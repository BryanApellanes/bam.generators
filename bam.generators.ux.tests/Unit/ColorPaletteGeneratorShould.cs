using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux;
using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("ColorPaletteGenerator should", Selector = "cpgut")]
    public class ColorPaletteGeneratorShould : UnitTestMenuContainer
    {
        public ColorPaletteGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ReturnThemeForEachIndustry()
        {
            string[] industries = { "healthcare", "retail", "construction", "legal", "energy", "manufacturing" };

            When.A<string[]>("suggests palette for each industry",
                industries,
                (inds) =>
                {
                    foreach (string industry in inds)
                    {
                        UxThemeModel theme = ColorPaletteGenerator.Suggest("Test Company", industry);
                        if (string.IsNullOrEmpty(theme.PrimaryColor)) return null!;
                    }
                    return inds;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("all industries returned valid themes", because.Result != null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReturnValidHexColors()
        {
            When.A<UxThemeModel>("suggests palette with valid hex colors",
                () => ColorPaletteGenerator.Suggest("Acme Clinic", "healthcare"),
                (theme) => theme)
            .TheTest
            .ShouldPass(because =>
            {
                UxThemeModel theme = (UxThemeModel)because.Result;
                because.ItsTrue("primary color is valid hex", IsValidHex(theme.PrimaryColor));
                because.ItsTrue("secondary color is valid hex", IsValidHex(theme.SecondaryColor));
                because.ItsTrue("accent color is valid hex", IsValidHex(theme.AccentColor));
                because.ItsTrue("background color is valid hex", IsValidHex(theme.BackgroundColor));
                because.ItsTrue("text color is valid hex", IsValidHex(theme.TextColor));
                because.ItsTrue("heading color is valid hex", IsValidHex(theme.HeadingColor));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ProduceDifferentPalettesForDifferentCompanies()
        {
            When.A<UxThemeModel[]>("suggests different palettes for different company names",
                () => new[]
                {
                    ColorPaletteGenerator.Suggest("Acme Clinic", "healthcare"),
                    ColorPaletteGenerator.Suggest("Beta Medical", "healthcare")
                },
                (themes) => themes)
            .TheTest
            .ShouldPass(because =>
            {
                UxThemeModel[] themes = (UxThemeModel[])because.Result;
                because.ItsTrue("different companies produce different primary colors",
                    themes[0].PrimaryColor != themes[1].PrimaryColor);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ProduceDeterministicResults()
        {
            When.A<UxThemeModel[]>("suggests same palette for same input",
                () => new[]
                {
                    ColorPaletteGenerator.Suggest("Acme Clinic", "healthcare"),
                    ColorPaletteGenerator.Suggest("Acme Clinic", "healthcare")
                },
                (themes) => themes)
            .TheTest
            .ShouldPass(because =>
            {
                UxThemeModel[] themes = (UxThemeModel[])because.Result;
                because.ItsTrue("same input produces same primary color",
                    themes[0].PrimaryColor == themes[1].PrimaryColor);
                because.ItsTrue("same input produces same secondary color",
                    themes[0].SecondaryColor == themes[1].SecondaryColor);
                because.ItsTrue("same input produces same accent color",
                    themes[0].AccentColor == themes[1].AccentColor);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FallbackToLegalForUnknownIndustry()
        {
            When.A<UxThemeModel>("suggests palette for unknown industry",
                () => ColorPaletteGenerator.Suggest("Test Co", "unknown-industry"),
                (theme) => theme)
            .TheTest
            .ShouldPass(because =>
            {
                UxThemeModel theme = (UxThemeModel)because.Result;
                because.ItsTrue("returns non-null theme", theme != null);
                because.ItsTrue("primary color is valid hex", IsValidHex(theme!.PrimaryColor));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static bool IsValidHex(string color)
        {
            return !string.IsNullOrEmpty(color)
                && color.StartsWith("#")
                && color.Length == 7;
        }
    }
}
