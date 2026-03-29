namespace Roblox.Services;

public class ApplicationService : ServiceBase, IService
{
    public enum GenerationContext
    {
        ApplicationCreation = 1,
        PasswordReset,
    }
    private IReadOnlyList<string> verificationWords { get; } = new List<string>()
    {
        "one",
        "two",
        "three",
        "four",
        "five",
        "six",
        "seven",
        "eight",
        "nine",
        "ten",
        "hat",
        "dog",
        "cat",
        "happy",
        "sad",
        "angry",
        "scared",
        "sleepy",
        "hungry",
        "parrot",
        "bird",
        "fish",
        "cow",
        "computer",
        "car",
        "bus",
        "train",
        "plane",
        "boat",
        "ship",
        "rocket",
        "spaceship",
        "wheel",
        "wheelbarrow",
        "truck",
        "bike",
        "house",
        "tree",
        "flower",
        "cloud",
        "sun",
        "moon",
        "star",
        "rain",
        "snow",
        "wind",
        "fire",
        "water",
        "earth",
        "sunshine",
        "moonlight",
        "starlight",
        "rainbow",
        "snowflake",
        "windstorm",
        "firestorm",
        "waterfall",
        "earthquake",
        "sunburn",
        "laptop",
        "desktop",
        "tablet",
        "cellphone",
        "smartphone",
        "television",
        "camera",
        "microwave",
        "oven",
        "toaster",
        "blender",
        "coffee",
        "tea",
        "milk",
        "water",
        "juice",
        "soda",
        "red",
        "green",
        "blue",
        "yellow",
        "orange",
        "purple",
        "pink",
        "brown",
        "black",
        "white",
        "grey",
        "gray",
        "silver",
        "gold",
        "platinum",
        "diamond",
        "emerald",
        "ruby",
        "sapphire",
        "topaz",
        "amethyst",
        "quartz",
        "lapis",
        "aluminium",
        "steel",
        "copper",
        "tin",
        "bronze",
        "silver",
        "suspicious",
        "suspect",
        "suspiciously",
        "night",
        "woods",
        "wood",
        "may",
        "trash",
        "mammal",
        "raccoon",
        "fox",
        "possum",
        "springs",
        "spring",
        "fall",
        "autumn",
        "summer",
        "winter",
        "drums",
        "drum",
        "guitar",
        "bass",
        "bee",
        "hololive",
        "medi",
        "skyler",
        "kar",
    }.Distinct().ToList();

    private int GetWordCount(GenerationContext ctx)
    {
        switch (ctx)
        {
            case GenerationContext.ApplicationCreation:
                return 6;
            case GenerationContext.PasswordReset:
                return 12;
            default:
                throw new NotImplementedException();
        }
    }

    public string GenerateVerificationPhrase(GenerationContext context)
    {
        var wordCount = GetWordCount(context);
        var random = new Random();
        var phrases = new List<string>();
        for (var i = 0; i < wordCount; i++)
        {
            var word = verificationWords[random.Next(verificationWords.Count)];
            phrases.Add(word);
        }
        return "marine " + string.Join(" ", phrases);
    }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return true;
    }
}