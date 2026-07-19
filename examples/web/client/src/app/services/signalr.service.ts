import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection;
  public messageReceived$ = new Subject<{ sourceType: string, message: string }>();
  public errorReceived$ = new Subject<{ sourceType: string, error: string }>();

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
  }

  public startConnection(): void {
    if (this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      this.hubConnection
        .start()
        .then(() => console.log('SignalR Connection started'))
        .catch(err => console.log('Error while starting SignalR connection: ' + err));
    }
  }

  public sendMessage(sourceType: string, message: string): void {
    this.hubConnection.invoke('SendMessage', sourceType, message)
      .catch(err => console.error('Error sending message:', err));
  }
}
