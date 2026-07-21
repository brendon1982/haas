import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="max-w-4xl mx-auto space-y-12 py-12">
      <div class="text-center space-y-4">
        <h1 class="text-5xl font-black text-gray-900 tracking-tight">
          Welcome to <span class="text-indigo-600">HaaS Web Hub</span>
        </h1>
        <p class="text-xl text-gray-600 max-w-2xl mx-auto">
          Explore the power of the Enterprise AI Harness through real-time interactive examples.
        </p>
      </div>

      <div class="grid md:grid-cols-2 gap-8">
        <div class="bg-white p-8 rounded-3xl shadow-xl border border-gray-100 hover:shadow-2xl transition-all group cursor-pointer" routerLink="/chat">
          <div class="h-14 w-14 bg-indigo-100 rounded-2xl flex items-center justify-center text-indigo-600 mb-6 group-hover:bg-indigo-600 group-hover:text-white transition-colors">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
            </svg>
          </div>
          <h2 class="text-2xl font-bold mb-3">AI Chat</h2>
          <p class="text-gray-500 leading-relaxed mb-6">
            Engage in long-running asynchronous conversations with AI assistants. Built on the HaaS Queued Engine with SignalR.
          </p>
          <span class="text-indigo-600 font-bold flex items-center gap-2 group-hover:translate-x-2 transition-transform">
            Try AI Chat
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7l5 5m0 0l-5 5m5-5H6" />
            </svg>
          </span>
        </div>

        <div class="bg-white p-8 rounded-3xl shadow-xl border border-gray-100 hover:shadow-2xl transition-all group cursor-pointer" routerLink="/tictactoe">
          <div class="h-14 w-14 bg-rose-100 rounded-2xl flex items-center justify-center text-rose-600 mb-6 group-hover:bg-rose-600 group-hover:text-white transition-colors">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16m-7 6h7" />
            </svg>
          </div>
          <h2 class="text-2xl font-bold mb-3">Tic-Tac-Toe</h2>
          <p class="text-gray-500 leading-relaxed mb-6">
            Play a classic game against a HaaS-driven AI agent. Demonstrates tool execution and per-session state management.
          </p>
          <span class="text-rose-600 font-bold flex items-center gap-2 group-hover:translate-x-2 transition-transform">
            Play Game
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7l5 5m0 0l-5 5m5-5H6" />
            </svg>
          </span>
        </div>
      </div>
    </div>
  `
})
export class HomeComponent {}
