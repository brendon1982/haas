import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SignalRService } from '../../services/signalr.service';
import { Subscription } from 'rxjs';

interface Message {
  text: string;
  sender: 'user' | 'ai' | 'system';
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html'
})
export class ChatComponent implements OnInit, OnDestroy {
  public messages: Message[] = [];
  public newMessage: string = '';
  private subscription: Subscription = new Subscription();

  constructor(private signalRService: SignalRService) {}

  ngOnInit(): void {
    this.signalRService.startConnection();
    
    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.messages.push({ text: data.message, sender: 'ai' });
        }
      })
    );

    this.subscription.add(
      this.signalRService.errorReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.messages.push({ text: `Error: ${data.error}`, sender: 'system' });
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  public sendMessage(): void {
    if (this.newMessage.trim()) {
      this.messages.push({ text: this.newMessage, sender: 'user' });
      this.signalRService.sendMessage('chat', this.newMessage);
      this.newMessage = '';
    }
  }
}
