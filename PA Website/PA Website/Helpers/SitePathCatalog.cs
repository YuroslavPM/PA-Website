using PA_Website.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace PA_Website.Helpers;

public static class SitePathCatalog
{
    public const string ProgramEmail = "dushevnamozaika@gmail.com";

    public static IReadOnlyList<PathCard> Create(IUrlHelper url) =>
    [
        new PathCard
        {
            Title = "Психологично консултиране",
            Description = "Индивидуално пространство за разговор, осмисляне и подкрепа.",
            ImagePath = "/Images/psy.png",
            Url = url.Action("Psychology", "Services") ?? "/Services/Psychology"
        },
        new PathCard
        {
            Title = "Астрологични услуги",
            Description = "Поглед към вътрешния свят през символния език на астрологията.",
            ImagePath = "/Images/articles/ast.jpg",
            Url = url.Action("Astrology", "Services") ?? "/Services/Astrology"
        },
        new PathCard
        {
            Title = "Авторска програма „Пътешествие към себе си“",
            Description = "8-седмична авторска програма по терапевтично писане за себепознание и личностно развитие.",
            ImagePath = "/Images/siteImg/program-writing.png",
            Url = url.Action("Program", "Home") ?? "/Home/Program"
        },
        new PathCard
        {
            Title = "Групи и събития",
            Description = "Виж предстоящите групи, срещи и събития.",
            ImagePath = "/Images/siteImg/events-coming-soon.png",
            Url = url.Action("Events", "Home") ?? "/Home/Events"
        }
    ];
}
