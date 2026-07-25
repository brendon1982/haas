import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { ChatComponent } from './chat.component';
import { ChatSignalRService } from '../../services/chat-signalr.service';

describe('ChatComponent', () => {
  let component: ChatComponent;
  let fixture: ComponentFixture<ChatComponent>;
  let processingStarted$: Subject<{ sourceType: string; messageId?: string }>;
  let messageReceived$: Subject<{ sourceType: string; message: string; messageId?: string }>;

  beforeEach(async () => {
    processingStarted$ = new Subject();
    messageReceived$ = new Subject();
    const signalRService = {
      connectionState: signal('Connected'),
      messageReceived$,
      errorReceived$: new Subject<{ sourceType: string; error: string }>(),
      processingStarted$,
      startConnection: vi.fn(),
      sendMessage: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [{ provide: ChatSignalRService, useValue: signalRService }],
    }).compileComponents();

    fixture = TestBed.createComponent(ChatComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows the working placeholder when processing starts', () => {
    processingStarted$.next({ sourceType: 'chat', messageId: 'working-message' });
    fixture.detectChanges();

    expect(component.messages()).toEqual([
      { id: 'working-message', text: 'Working...', sender: 'ai', isThinking: true },
    ]);
    expect(fixture.nativeElement.textContent).toContain('Working...');
  });

  it('keeps the user position when a response arrives after scrolling up', () => {
    const scrollElement = fixture.nativeElement.querySelector('[role="log"]') as HTMLDivElement;
    let scrollHeight = 100;
    let scrollTop = 0;

    Object.defineProperties(scrollElement, {
      clientHeight: { configurable: true, get: () => 100 },
      scrollHeight: { configurable: true, get: () => scrollHeight },
      scrollTop: {
        configurable: true,
        get: () => scrollTop,
        set: (value: number) => {
          scrollTop = value;
        },
      },
    });

    scrollElement.dispatchEvent(new Event('scroll'));
    scrollHeight = 300;
    scrollTop = 20;
    scrollElement.dispatchEvent(new Event('scroll'));
    scrollHeight = 400;
    messageReceived$.next({ sourceType: 'chat', message: 'A response', messageId: 'response-message' });
    fixture.detectChanges();

    expect(scrollTop).toBe(20);
  });
});
