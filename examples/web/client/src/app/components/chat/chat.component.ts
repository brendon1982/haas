import { afterRenderEffect, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { ChatSignalRService } from '../../services/chat-signalr.service';
import { Subscription } from 'rxjs';

interface Message {
  id: string;
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
export class ChatComponent implements OnInit, OnDestroy {
  private signalRService = inject(ChatSignalRService);
  private scrollContainer = viewChild<ElementRef<HTMLDivElement>>('scrollContainer');
  
  public messages = signal<Message[]>([]);
  public newMessageControl = new FormControl('', { nonNullable: true });
  public isThinking = signal<boolean>(false);
  
  public connectionStatus = computed(() => {
    return this.signalRService.connectionState();
  });
  
  private subscription: Subscription = new Subscription();
  private shouldScrollToBottom: boolean = true;
  private readonly scrollEffect = afterRenderEffect(() => {
    this.messages();
    this.scrollToBottom();
  });

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
              { id: crypto.randomUUID(), text: data.message, sender: 'ai' }
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
            { id: data.messageId ?? crypto.randomUUID(), text: 'Working...', sender: 'ai', isThinking: true }
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
            { id: crypto.randomUUID(), text: `Error: ${data.error}`, sender: 'system' }
          ]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  public onScroll(): void {
    const container = this.scrollContainer();
    if (!container) return;
    const element = container.nativeElement;
    this.shouldScrollToBottom = this.isAtBottom(element);
  }

  private scrollToBottom(): void {
    const container = this.scrollContainer();
    if (!this.shouldScrollToBottom || !container) {
      return;
    }

    const element = container.nativeElement;
    element.scrollTop = element.scrollHeight;
  }

  private isAtBottom(element: HTMLDivElement): boolean {
    return element.scrollHeight - element.scrollTop <= element.clientHeight + 50;
  }

  public sendMessage(): void {
    const text = this.newMessageControl.value.trim();
    if (text && this.connectionStatus() === 'Connected') {
      this.shouldScrollToBottom = true;
      this.messages.update(msgs => [...msgs, { id: crypto.randomUUID(), text: text, sender: 'user' }]);
      this.signalRService.sendMessage(text);
      this.newMessageControl.setValue('');
    }
  }
}
