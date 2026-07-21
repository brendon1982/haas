import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SignalRService } from '../../services/signalr.service';
import { Subscription } from 'rxjs';

interface Message {
  text: string;
  sender: 'user' | 'ai' | 'system';
  isThinking?: boolean;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html'
})
export class ChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;
  
  public messages: Message[] = [];
  public newMessage: string = '';
  public isThinking: boolean = false;
  public connectionStatus: string = 'Connected';
  
  private subscription: Subscription = new Subscription();
  private shouldScrollToBottom: boolean = true;

  constructor(private signalRService: SignalRService) {}

  ngOnInit(): void {
    this.signalRService.startConnection();
    
    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.isThinking = false;
          // Remove any thinking placeholders
          this.messages = this.messages.filter(m => !m.isThinking);
          this.messages.push({ text: data.message, sender: 'ai' });
        }
      })
    );

    this.subscription.add(
      this.signalRService.processingStarted$.subscribe(sourceType => {
        if (sourceType === 'chat' && !this.isThinking) {
          this.isThinking = true;
          this.messages.push({ text: 'Working...', sender: 'ai', isThinking: true });
        }
      })
    );

    this.subscription.add(
      this.signalRService.errorReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.isThinking = false;
          this.messages = this.messages.filter(m => !m.isThinking);
          this.messages.push({ text: `Error: ${data.error}`, sender: 'system' });
        }
      })
    );

    this.subscription.add(
      this.signalRService.connectionState$.subscribe(state => {
        this.connectionStatus = state;
      })
    );
  }

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  public onScroll(): void {
    const element = this.scrollContainer.nativeElement;
    const atBottom = element.scrollHeight - element.scrollTop <= element.clientHeight + 50;
    this.shouldScrollToBottom = atBottom;
  }

  private scrollToBottom(): void {
    if (this.shouldScrollToBottom && this.scrollContainer) {
      try {
        this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
      } catch (err) {}
    }
  }

  public sendMessage(): void {
    if (this.newMessage.trim() && this.connectionStatus === 'Connected') {
      this.messages.push({ text: this.newMessage, sender: 'user' });
      this.signalRService.sendMessage('chat', this.newMessage);
      this.newMessage = '';
      // We don't set isThinking here, we wait for the ProcessingStarted signal from server
      // But we can set a local flag if we want immediate feedback, 
      // though the server should be fast to respond with "Processing"
    }
  }
}
