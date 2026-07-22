import { Injectable } from '@angular/core';
import { BaseSignalRService } from './base-signalr.service';

@Injectable({
  providedIn: 'root'
})
export class ChatSignalRService extends BaseSignalRService {
  constructor() {
    super('http://localhost:5000/chatHub', 'Chat');
  }

  public sendMessage(message: string): void {
    this.hubConnection.invoke('SendMessage', message)
      .catch(err => console.error('Error sending message:', err));
  }
}
