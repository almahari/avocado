namespace Avocado;

public readonly record struct AppSize(double Width, double Height, double Scale);

public static class AppSizeLogic
{
    public static readonly AppSize Sleeping = new(126, 162, 0.3);

    public static AppSize Get(bool small) => small
        ? new AppSize(210, 270, 0.5)
        : new AppSize(420, 540, 1.0);
}
