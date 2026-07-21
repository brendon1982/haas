import { Injectable, signal, WritableSignal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection;
  public messageReceived$ = new Subject<{ sourceType: string, message: string, messageId?: string }>();
  public errorReceived$ = new Subject<{ sourceType: string, error: string }>();
  public boardUpdated$ = new Subject<string[]>();
  public processingStarted$ = new Subject<{ sourceType: string, messageId?: string }>();
  
  private _connectionState = signal<signalR.HubConnectionState>(signalR.HubConnectionState.Disconnected);
  public connectionState = this._connectionState.asReadonly();
  
  public reconnected$ = new Subject<void>();

  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/haasHub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveMessage', (sourceType: string, message: string, messageId?: string) => {
      this.messageReceived$.next({ sourceType, message, messageId });
    });

    this.hubConnection.on('ReceiveError', (sourceType: string, error: string) => {
      this.errorReceived$.next({ sourceType, error });
    });

    this.hubConnection.on('BoardUpdated', (board: string[]) => {
      this.boardUpdated$.next(board);
    });

    this.hubConnection.on('ProcessingStarted', (sourceType: string, messageId?: string) => {
      this.processingStarted$.next({ sourceType, messageId });
    });

    this.hubConnection.onreconnecting(() => {
      this._connectionState.set(signalR.HubConnectionState.Reconnecting);
    });

    this.hubConnection.onreconnected(() => {
      this._connectionState.set(signalR.HubConnectionState.Connected);
      this.reconnected$.next();
    });

    this.hubConnection.onclose(() => {
      this._connectionState.set(signalR.HubConnectionState.Disconnected);
    });
  }

  public startConnection(): void {
    if (this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      this.hubConnection
        .start()
        .then(() => {
          console.log('SignalR Connection started');
          this._connectionState.set(signalR.HubConnectionState.Connected);
          // Initial state request
          this.hubConnection.invoke('ResetGame').catch(err => console.error(err));
        })
        .catch(err => {
          console.log('Error while starting SignalR connection: ' + err);
          this._connectionState.set(signalR.HubConnectionState.Disconnected);
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
