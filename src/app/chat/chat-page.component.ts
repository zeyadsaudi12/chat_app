import {
  Component, ElementRef, OnDestroy, OnInit, computed, effect, inject, signal, viewChild
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { BadgeModule } from 'primeng/badge';
import { AvatarModule } from 'primeng/avatar';

import { ChatStore } from './chat.store';
import { ChatApiService } from '../core/chat-api.service';
import { ChatRealtimeService } from '../core/chat-realtime.service';
import { AuthService } from '../core/auth.service';
import { ChatAttachment, ChatConversation, ChatMessage, UserSummary } from './chat.models';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [DatePipe, FormsModule, ButtonModule, InputTextModule, TextareaModule, BadgeModule, AvatarModule],
  template: `
    <div class="chat-layout">
      <aside class="sidebar">
        <div class="sidebar-head">
          <h2>{{ auth.displayName() }}</h2>
          <p-button icon="pi pi-plus" size="small" [text]="true" (onClick)="showNew.set(!showNew())" aria-label="New chat" />
          <p-button icon="pi pi-sign-out" size="small" [text]="true" severity="secondary" (onClick)="logout()" aria-label="Sign out" />
        </div>

        @if (showNew()) {
          <div class="new-panel">
            <input pInputText [(ngModel)]="userQuery" (input)="searchUsers()" placeholder="Find people..." />
            @for (u of userResults(); track u.id) {
              <div class="user-row">
                <span>{{ u.displayName }}</span>
                <p-button label="Chat" size="small" [text]="true" (onClick)="startPrivate(u)" />
                <p-button [label]="isPicked(u) ? 'Added' : 'Group'" size="small" [text]="true"
                          severity="secondary" (onClick)="toggleMember(u)" />
              </div>
            }
            @if (groupMembers().length) {
              <input pInputText [(ngModel)]="groupTitle" placeholder="Group name" />
              <small>{{ groupNames() }}</small>
              <p-button label="Create group" size="small" (onClick)="createGroup()"
                        [disabled]="!groupTitle.trim()" />
            }
          </div>
        }

        @for (c of store.conversations(); track c.id) {
          <button class="conversation" [class.active]="store.activeConversationId() === c.id"
                  (click)="open(c.id)">
            <span class="conversation-title">{{ c.title }}</span>
            <small>{{ c.lastMessage || (c.lastAt ? 'Attachment' : 'Start chatting') }}</small>
            @if (c.unreadCount > 0) { <p-badge [value]="c.unreadCount.toString()" /> }
          </button>
        }
      </aside>

      <main class="chat-window">
        @if (store.activeConversation(); as conversation) {
          <header class="chat-header">
            <p-avatar icon="pi pi-comments" shape="circle" />
            <div>
              <strong>{{ conversation.title }}</strong>
              <small>{{ conversation.type === 'group' ? 'Group chat · ' + conversation.memberCount + ' members' : 'Private chat' }}</small>
            </div>
          </header>

          <section class="messages" #list>
            @if (store.hasMore()) {
              <p-button label="Load older messages" size="small" [text]="true" (onClick)="store.loadOlder()" />
            }
            @for (m of store.messages(); track m.id) {
              <article class="message" [class.mine]="m.senderId === me()">
                @if (conversation.type === 'group' && m.senderId !== me()) { <small>{{ m.senderName }}</small> }
                @if (m.content) { <p>{{ m.content }}</p> }
                @for (f of m.attachments; track f.id) {
                  <button class="file" (click)="download(f)">📎 {{ f.fileName }} ({{ sizeOf(f.size) }})</button>
                }
                <time>
                  {{ m.sentAt | date:'shortTime' }}
                  @if (m.senderId === me()) {
                    <span [attr.aria-label]="isRead(m, conversation) ? 'Read' : 'Delivered'">
                      {{ isRead(m, conversation) ? '✓✓' : '✓' }}
                    </span>
                  }
                </time>
              </article>
            }
          </section>

          @if (typingText()) { <div class="typing">{{ typingText() }}</div> }
          @if (store.error()) { <div class="error">{{ store.error() }}</div> }

          <footer class="composer">
            <label class="attach">
              📎<input type="file" hidden (change)="selectFile($event)" />
            </label>
            <textarea pTextarea [(ngModel)]="draft" (input)="typing()" (keydown.enter)="onEnter($event)"
                      [autoResize]="true" placeholder="Write a message..." rows="1"></textarea>
            <p-button label="Send" icon="pi pi-send"
                      [disabled]="!draft.trim() && !pendingAttachment()" (onClick)="send()" />
          </footer>
          @if (pendingAttachment(); as p) {
            <div class="pending">📎 {{ p.fileName }}
              <button class="link" (click)="pendingAttachment.set(null)">remove</button>
            </div>
          }
        } @else {
          <div class="empty-state">Select a conversation or start a new one.</div>
        }
      </main>
    </div>
  `,
  styles: [`
    :host { display: block; padding: 16px; }
    .chat-layout { display: grid; grid-template-columns: 300px minmax(0, 1fr); height: 88vh; min-height: 450px;
      border: 1px solid var(--p-content-border-color); border-radius: 12px; overflow: hidden; }
    .sidebar { padding: 12px; border-right: 1px solid var(--p-content-border-color); overflow-y: auto; }
    .sidebar-head { display: flex; align-items: center; gap: 4px; }
    .sidebar-head h2 { flex: 1; font-size: 1.1rem; margin: 0; }
    .new-panel { display: flex; flex-direction: column; gap: 8px; margin: 8px 0 12px; }
    .user-row { display: flex; align-items: center; gap: 4px; }
    .user-row span { flex: 1; }
    .conversation { display: flex; flex-direction: column; align-items: flex-start; gap: 4px; width: 100%;
      padding: 12px; margin-bottom: 6px; border: 0; border-radius: 8px; text-align: left;
      background: transparent; color: inherit; cursor: pointer; }
    .conversation.active { background: var(--p-content-hover-background); }
    .conversation small, .chat-header small { opacity: .7; }
    .chat-window { display: flex; flex-direction: column; min-width: 0; }
    .chat-header { display: flex; align-items: center; gap: 12px; padding: 16px;
      border-bottom: 1px solid var(--p-content-border-color); }
    .chat-header div { display: flex; flex-direction: column; gap: 4px; }
    .messages { display: flex; flex: 1; flex-direction: column; gap: 12px; padding: 16px; overflow-y: auto; }
    .message { align-self: flex-start; max-width: 75%; padding: 10px 14px; border-radius: 12px;
      background: var(--p-content-hover-background); overflow-wrap: anywhere; }
    .message.mine { align-self: flex-end; background: var(--p-primary-color); color: var(--p-primary-contrast-color); }
    .message p { margin: 4px 0; white-space: pre-wrap; }
    .message time { display: block; font-size: .7rem; opacity: .75; text-align: right; }
    .file, .link { background: none; border: 0; cursor: pointer; color: inherit; text-decoration: underline; padding: 0; display: block; }
    .composer { display: flex; align-items: flex-end; gap: 8px; padding: 12px; border-top: 1px solid var(--p-content-border-color); }
    .composer textarea { flex: 1; min-width: 0; }
    .attach { cursor: pointer; font-size: 1.3rem; padding: 6px; }
    .pending { padding: 0 16px 12px; font-size: .85rem; }
    .typing, .empty-state { padding: 16px; opacity: .7; }
    .error { padding: 8px 16px; color: var(--p-red-500); }
    @media (max-width: 700px) { .chat-layout { grid-template-columns: 120px minmax(0, 1fr); } }
  `]
})
export class ChatPageComponent implements OnInit, OnDestroy {
  readonly store = inject(ChatStore);
  readonly auth = inject(AuthService);
  private realtime = inject(ChatRealtimeService);
  private api = inject(ChatApiService);
  private router = inject(Router);

  readonly me = this.auth.userId;
  readonly list = viewChild<ElementRef<HTMLElement>>('list');

  draft = '';
  userQuery = '';
  groupTitle = '';
  readonly showNew = signal(false);
  readonly userResults = signal<UserSummary[]>([]);
  readonly groupMembers = signal<UserSummary[]>([]);
  readonly groupNames = computed(() => this.groupMembers().map(u => u.displayName).join(', '));
  readonly pendingAttachment = signal<ChatAttachment | null>(null);
  readonly typingUsers = signal<Record<string, string>>({});
  readonly typingText = computed(() => {
    const names = Object.values(this.typingUsers());
    return names.length ? `${names.join(', ')} ${names.length > 1 ? 'are' : 'is'} typing...` : '';
  });

  private subs = new Subscription();
  private typingTimer?: ReturnType<typeof setTimeout>;
  private typingExpiry = new Map<string, ReturnType<typeof setTimeout>>();
  private searchTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    // Scroll to the bottom only when a new newest message arrives (not when older ones are prepended).
    effect(() => {
      this.store.messages().at(-1)?.id;
      queueMicrotask(() => {
        const el = this.list()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  async ngOnInit() {
    this.subs.add(this.realtime.messages$.subscribe(m => {
      this.store.addMessage(m, this.me());
      if (m.conversationId === this.store.activeConversationId() && m.senderId !== this.me())
        void this.realtime.markRead(m.conversationId);
      this.setTypingUser(m.senderId, '', false);
    }));

    this.subs.add(this.realtime.typing$.subscribe(e => {
      if (e.conversationId === this.store.activeConversationId() && e.userId !== this.me())
        this.setTypingUser(e.userId, e.userName, e.isTyping);
    }));

    this.subs.add(this.realtime.receipts$.subscribe(r => this.store.applyReceipt(r)));

    this.subs.add(this.realtime.conversationAdded$.subscribe(c => {
      this.store.upsertConversation(c);
      void this.realtime.joinConversation(c.id);
    }));

    this.subs.add(this.realtime.reconnected$.subscribe(async () => {
      await this.store.loadConversations();           // resync anything missed while offline
      const id = this.store.activeConversationId();
      if (id) { await this.store.openConversation(id); await this.realtime.markRead(id); }
    }));

    try {
      await this.realtime.connect();
    } catch {
      this.store.setError('Could not connect to chat. Check the API is running.');
    }
    await this.store.loadConversations();
  }

  async open(id: string) {
    this.typingUsers.set({});
    this.pendingAttachment.set(null);
    await this.store.openConversation(id);
    try {
      await this.realtime.joinConversation(id);
      await this.realtime.markRead(id);
    } catch { /* connection may be down; reconnect handler resyncs */ }
  }

  async send() {
    const id = this.store.activeConversationId();
    const content = this.draft.trim();
    const attachment = this.pendingAttachment();
    if (!id || (!content && !attachment)) return;

    try {
      await this.realtime.sendMessage(id, content, attachment ? [attachment.id] : []);
      this.draft = '';
      this.pendingAttachment.set(null);
      this.store.setError(null);
      clearTimeout(this.typingTimer);
      await this.realtime.setTyping(id, false);
    } catch {
      this.store.setError('Message could not be sent. Try again.');
    }
  }

  async typing() {
    const id = this.store.activeConversationId();
    if (!id) return;
    try { await this.realtime.setTyping(id, true); } catch { return; }
    clearTimeout(this.typingTimer);
    this.typingTimer = setTimeout(() => void this.realtime.setTyping(id, false), 1500);
  }

  onEnter(event: Event) {
    const e = event as KeyboardEvent;
    if (!e.shiftKey) { e.preventDefault(); void this.send(); }
  }

  async selectFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 10 * 1024 * 1024) { this.store.setError('Maximum file size is 10 MB.'); return; }

    try {
      this.pendingAttachment.set(await firstValueFrom(this.api.upload(file)));
      this.store.setError(null);
    } catch (e: any) {
      this.store.setError(typeof e?.error === 'string' ? e.error : 'File upload failed.');
    }
  }

  async download(file: ChatAttachment) {
    try {
      const blob = await firstValueFrom(this.api.download(file.id));
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = file.fileName; a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.store.setError('Download failed.');
    }
  }

  isRead(m: ChatMessage, c: ChatConversation) {
    return m.readBy.length >= Math.max(1, c.memberCount - 1);
  }

  sizeOf(bytes: number) {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  searchUsers() {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(async () => {
      try { this.userResults.set(await firstValueFrom(this.api.searchUsers(this.userQuery))); } catch { /* ignore */ }
    }, 250);
  }

  isPicked(u: UserSummary) { return this.groupMembers().some(x => x.id === u.id); }

  toggleMember(u: UserSummary) {
    this.groupMembers.set(this.isPicked(u)
      ? this.groupMembers().filter(x => x.id !== u.id)
      : [...this.groupMembers(), u]);
  }

  async startPrivate(u: UserSummary) {
    try {
      const c = await firstValueFrom(this.api.createPrivate(u.id));
      this.store.upsertConversation(c);
      this.resetNewPanel();
      await this.open(c.id);
    } catch { this.store.setError('Could not start the chat.'); }
  }

  async createGroup() {
    try {
      const c = await firstValueFrom(this.api.createGroup(this.groupTitle, this.groupMembers().map(u => u.id)));
      this.store.upsertConversation(c);
      this.resetNewPanel();
      await this.open(c.id);
    } catch { this.store.setError('Could not create the group.'); }
  }

  async logout() {
    await this.realtime.disconnect();
    this.auth.logout();
    await this.router.navigateByUrl('/login');
  }

  private resetNewPanel() {
    this.showNew.set(false); this.userQuery = ''; this.groupTitle = '';
    this.userResults.set([]); this.groupMembers.set([]);
  }

  private setTypingUser(userId: string, name: string, on: boolean) {
    clearTimeout(this.typingExpiry.get(userId));
    const next = { ...this.typingUsers() };
    if (on) {
      next[userId] = name;
      // Safety net in case the "stopped typing" event is lost.
      this.typingExpiry.set(userId, setTimeout(() => this.setTypingUser(userId, '', false), 4000));
    } else {
      delete next[userId];
    }
    this.typingUsers.set(next);
  }

  ngOnDestroy() {
    clearTimeout(this.typingTimer);
    clearTimeout(this.searchTimer);
    this.typingExpiry.forEach(t => clearTimeout(t));
    this.subs.unsubscribe();
  }
}
