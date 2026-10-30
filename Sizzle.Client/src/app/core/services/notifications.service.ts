import { Injectable, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { RecipeCreatedEvent } from '../models';

/** Listens on /hubs/recipes for new recipes from users the current user follows. */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly items = signal<RecipeCreatedEvent[]>([]);

  constructor() {
    effect(() => {
      const token = this.auth.token();
      void this.stop().then(() => (token ? this.start(token) : undefined));
    });
  }

  dismiss(recipeId: string): void {
    this.items.update((items) => items.filter((item) => item.recipeId !== recipeId));
  }

  private async start(token: string): Promise<void> {
    const connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/recipes`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('RecipeCreated', (event: RecipeCreatedEvent) =>
      this.items.update((items) => [event, ...items]),
    );
    this.connection = connection;
    try {
      await connection.start();
    } catch (err) {
      console.warn('SignalR connection failed', err);
    }
  }

  private async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.items.set([]);
    await connection?.stop();
  }
}
