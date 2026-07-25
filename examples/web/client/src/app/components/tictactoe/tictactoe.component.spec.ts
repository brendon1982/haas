import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { TicTacToeComponent } from './tictactoe.component';
import { TicTacToeSignalRService, TicTacToeState } from '../../services/tictactoe-signalr.service';

describe('TicTacToeComponent', () => {
  let component: TicTacToeComponent;
  let fixture: ComponentFixture<TicTacToeComponent>;
  let boardUpdated$: Subject<TicTacToeState>;

  beforeEach(async () => {
    boardUpdated$ = new Subject<TicTacToeState>();
    const signalRService = {
      connectionState: signal('Connected'),
      messageReceived$: new Subject<{ sourceType: string; message: string; messageId?: string }>(),
      errorReceived$: new Subject<{ sourceType: string; error: string }>(),
      processingStarted$: new Subject<{ sourceType: string; messageId?: string }>(),
      reconnected$: new Subject<void>(),
      boardUpdated$,
      startConnection: vi.fn(),
      sendMove: vi.fn(),
      resetGame: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [TicTacToeComponent],
      providers: [{ provide: TicTacToeSignalRService, useValue: signalRService }],
    }).compileComponents();

    fixture = TestBed.createComponent(TicTacToeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows the player win state and a new-game action', () => {
    boardUpdated$.next({
      board: ['X', 'X', 'X', 'O', ' ', ' ', ' ', ' ', ' '],
      winner: 'X',
      isDraw: false,
      isGameOver: true,
    });
    fixture.detectChanges();

    expect(component.winner()).toBe('X');
    expect(component.displayStatus()).toBe('You win!');
    expect(fixture.nativeElement.textContent).toContain('Play new game');
  });

  it('does not allow moves after the game ends', () => {
    boardUpdated$.next({
      board: ['O', 'O', 'O', 'X', 'X', ' ', ' ', ' ', ' '],
      winner: 'O',
      isDraw: false,
      isGameOver: true,
    });
    component.makeMove(5);

    expect(component.signalRService.sendMove).not.toHaveBeenCalled();
  });
});
