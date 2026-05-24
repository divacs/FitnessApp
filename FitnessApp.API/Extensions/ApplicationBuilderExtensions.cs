namespace FitnessApp.API.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);

        app.UseAuthorization();

        app.MapGet("/health", () => Results.Text("Healthy", "text/plain"))
            .WithName("HealthCheck")
            .AllowAnonymous();

        app.MapControllers();

        return app;
    }
}
