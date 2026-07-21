import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection;
  public messageReceived$ = new Subject<{ sourceType: string, message: string }>();
  public errorReceived$ = new Subject<{ sourceType: string, error: string }>();
  public boardUpdated$ = new Subject<string[]>();
  public processingStarted$ = new Subject<string>();
  public connectionState$ = new BehaviorSubject<signalR.HubConnectionState>(signalR.HubConnectionState.Disconnected);
  public reconnected$ = new Subject<void>();

  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/haasHub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveMessage', (sourceType: string, message: string) => {
      this.messageReceived$.next({ sourceType, message });
    });

    this.hubConnection.on('ReceiveError', (sourceType: string, error: string) => {
      this.errorReceived$.next({ sourceType, error });
    });

    this.hubConnection.on('BoardUpdated', (board: string[]) => {
      this.boardUpdated$.next(board);
    });

    this.hubConnection.on('ProcessingStarted', (sourceType: string) => {
      this.processingStarted$.next(sourceType);
    });

    this.hubConnection.onreconnecting(() => {
      this.connectionState$.next(signalR.HubConnectionState.Reconnecting);
    });

    this.hubConnection.onreconnected(() => {
      this.connectionState$.next(signalR.HubConnectionState.Connected);
      this.reconnected$.next();
    });

    this.hubConnection.onclose(() => {
      this.connectionState$.next(signalR.HubConnectionState.Disconnected);
    });
  }

  public startConnection(): void {
    if (this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      this.hubConnection
        .start()
        .then(() => {
          console.log('SignalR Connection started');
          this.connectionState$.next(signalR.HubConnectionState.Connected);
          // Initial state request
          this.hubConnection.invoke('ResetGame').catch(err => console.error(err));
        })
        .catch(err => {
          console.log('Error while starting SignalR connection: ' + err);
          this.connectionState$.next(signalR.HubConnectionState.Disconnected);
        });
    }
  }

  public sendMessage(sourceType: string, message: string): void {
    this.hubConnection.invoke('SendMessage', sourceType, message)
      .catch(err => console.error('Error sending message:', err));
  }

  public sendMove(position: number): void {
    this.hubConnection.invoke('SendMove', position)
      .catch(err => console.error('Error sending move:', err));
  }

  public resetGame(): void {
    this.hubConnection.invoke('ResetGame')
      .catch(err => console.error('Error resetting game:', err));
  }
}
