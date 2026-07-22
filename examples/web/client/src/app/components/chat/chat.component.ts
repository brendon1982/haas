import { Component, OnInit, OnDestroy, ElementRef, AfterViewChecked, signal, computed, ChangeDetectionStrategy, inject, viewChild } from '@angular/core';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { ChatSignalRService } from '../../services/chat-signalr.service';
import { Subscription } from 'rxjs';

interface Message {
  id?: string;
  text: string;
  sender: 'user' | 'ai' | 'system';
  isThinking?: boolean;
}

@Component({
  selector: 'app-chat',
  imports: [ReactiveFormsModule],
  templateUrl: './chat.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  private signalRService = inject(ChatSignalRService);
  private scrollContainer = viewChild<ElementRef>('scrollContainer');
  
  public messages = signal<Message[]>([]);
  public newMessageControl = new FormControl('', { nonNullable: true });
  public isThinking = signal<boolean>(false);
  
  public connectionStatus = computed(() => {
    return this.signalRService.connectionState();
  });
  
  private subscription: Subscription = new Subscription();
  private shouldScrollToBottom: boolean = true;

  ngOnInit(): void {
    this.signalRService.startConnection();
    
    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.isThinking.set(false);
          // If we have an ID, replace the thinking placeholder with that ID
          if (data.messageId) {
            this.messages.update(msgs => msgs.map(m => 
              m.id === data.messageId 
                ? { ...m, text: data.message, isThinking: false } 
                : m
            ));
          } else {
            // Fallback: Remove all thinking placeholders and add new message
            this.messages.update(msgs => [
              ...msgs.filter(m => !m.isThinking),
              { text: data.message, sender: 'ai' }
            ]);
          }
        }
      })
    );

    this.subscription.add(
      this.signalRService.processingStarted$.subscribe(data => {
        if (data.sourceType === 'chat' && !this.isThinking()) {
          this.isThinking.set(true);
          this.messages.update(msgs => [
            ...msgs,
            { id: data.messageId, text: 'Working...', sender: 'ai', isThinking: true }
          ]);
        }
      })
    );

    this.subscription.add(
      this.signalRService.errorReceived$.subscribe(data => {
        if (data.sourceType === 'chat') {
          this.isThinking.set(false);
          this.messages.update(msgs => [
            ...msgs.filter(m => !m.isThinking),
            { text: `Error: ${data.error}`, sender: 'system' }
          ]);
        }
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
    const container = this.scrollContainer();
    if (!container) return;
    const element = container.nativeElement;
    const atBottom = element.scrollHeight - element.scrollTop <= element.clientHeight + 50;
    this.shouldScrollToBottom = atBottom;
  }

  private scrollToBottom(): void {
    const container = this.scrollContainer();
    if (this.shouldScrollToBottom && container) {
      try {
        container.nativeElement.scrollTop = container.nativeElement.scrollHeight;
      } catch (err) {}
    }
  }

  public sendMessage(): void {
    const text = this.newMessageControl.value.trim();
    if (text && this.connectionStatus() === 'Connected') {
      this.messages.update(msgs => [...msgs, { id: crypto.randomUUID(), text: text, sender: 'user' }]);
      this.signalRService.sendMessage(text);
      this.newMessageControl.setValue('');
    }
  }
}
