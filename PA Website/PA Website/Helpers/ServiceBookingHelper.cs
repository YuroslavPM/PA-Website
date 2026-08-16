namespace PA_Website.Helpers;

using PA_Website.Models;

public enum ServiceBookingKind
{
    Appointment,
    BirthData,
    AppointmentAndBirthData,
    DualBirthData
}

public static class ServiceBookingHelper
{
    public static bool IsAstrology(this Service service)
        => string.Equals(service.CategoryOfService, "астрология", StringComparison.OrdinalIgnoreCase);

    public static ServiceBookingKind GetBookingKind(this Service service)
    {
        var name = service.NameService?.ToLowerInvariant() ?? string.Empty;
        var isAstrology = service.IsAstrology();
        var isConsultation = name.Contains("консултация");
        var isPartner = name.Contains("партньор");

        if (isAstrology && isPartner)
            return ServiceBookingKind.DualBirthData;

        if (isAstrology && isConsultation)
            return ServiceBookingKind.AppointmentAndBirthData;

        if (isAstrology)
            return ServiceBookingKind.BirthData;

        return ServiceBookingKind.Appointment;
    }

    public static bool NeedsAppointment(this ServiceBookingKind kind)
        => kind is ServiceBookingKind.Appointment or ServiceBookingKind.AppointmentAndBirthData;

    public static bool NeedsBirthData(this ServiceBookingKind kind)
        => kind is ServiceBookingKind.BirthData
            or ServiceBookingKind.AppointmentAndBirthData
            or ServiceBookingKind.DualBirthData;

    public static bool NeedsDualBirthData(this ServiceBookingKind kind)
        => kind == ServiceBookingKind.DualBirthData;

    public static bool NeedsAppointment(this Service service)
        => service.GetBookingKind().NeedsAppointment();

    public static bool NeedsBirthData(this Service service)
        => service.GetBookingKind().NeedsBirthData();

    public static bool NeedsDualBirthData(this Service service)
        => service.GetBookingKind().NeedsDualBirthData();

    public static int GetDisplayOrder(this Service service)
    {
        var name = service.NameService?.ToLowerInvariant() ?? string.Empty;

        if (name.Contains("астропсихолог"))
            return 2;

        if (name.Contains("психологическ"))
            return 1;

        if (name.Contains("рожден") && (name.Contains("хороскоп") || name.Contains("прогноз")))
            return 3;

        if (name.Contains("партньор"))
            return 4;

        return 100;
    }

    public static IOrderedQueryable<Service> OrderForDisplay(this IQueryable<Service> services)
        => services
            .OrderBy(s => s.NameService.ToLower().Contains("астропсихолог") ? 2
                : s.NameService.ToLower().Contains("психологическ") ? 1
                : s.NameService.ToLower().Contains("рожден") ? 3
                : s.NameService.ToLower().Contains("партньор") ? 4
                : 100)
            .ThenBy(s => s.NameService);

    public static IEnumerable<Service> OrderForDisplay(this IEnumerable<Service> services)
        => services
            .OrderBy(s => s.GetDisplayOrder())
            .ThenBy(s => s.NameService);
}
