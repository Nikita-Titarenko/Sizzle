using Microsoft.OpenApi;

namespace Sizzle.WebApi.ServiceExtensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Sizzle API",
                Version = "v1",
                Description = """
                    ## Error responses

                    Handled errors return an array of errors (including validation errors):

                    ```json
                    [
                      {
                        "message": "Recipe not found",
                        "field": "",
                        "key": 0,
                        "parameters": {}
                      }
                    ]
                    ```

                    `key` is the numeric `ErrorKey` value; for example, `0` is `NotFound` and `6` is `BadRequest`.

                    ## Recipe notifications (SignalR)

                    Connect to `/hubs/recipes` using a JWT. The server sends the `RecipeCreated` event to users following the recipe author. The event payload is:

                    ```json
                    {
                      "recipeId": "00000000-0000-0000-0000-000000000000",
                      "recipeName": "Recipe name",
                      "authorId": "00000000-0000-0000-0000-000000000000",
                      "authorName": "Author name",
                      "createdAt": "2026-10-30T12:00:00Z"
                    }
                    ```

                    JavaScript client (install `@microsoft/signalr`):

                    ```js
                    import * as signalR from "@microsoft/signalr";

                    const connection = new signalR.HubConnectionBuilder()
                      .withUrl("https://localhost:7104/hubs/recipes", {
                        accessTokenFactory: () => jwtToken
                      })
                      .withAutomaticReconnect()
                      .build();

                    connection.on("RecipeCreated", notification => {
                      console.log(notification.recipeName, notification.authorName);
                    });

                    await connection.start();
                    ```

                    Replace the URL with your API origin and `jwtToken` with the signed-in user's JWT.
                    """
            });
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Sizzle.WebApi.xml"));
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
