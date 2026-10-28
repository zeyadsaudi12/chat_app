import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ChatApiService } from '../core/chat-api.service';
import { ChatConversation, ChatMessage, ReadReceipt } from './chat.models';

interface ChatState {
  conversations: ChatConversation[];
  messages: ChatMessage[];
  activeConversationId: string | null;
  hasMore: boolean;
  loading: boolean;
  error: string | null;
}

const PAGE = 50;

const byRecent = (a: ChatConversation, b: ChatConversation) =>
  (b.lastAt ?? '').localeCompare(a.lastAt ?? '');

export const ChatStore = signalStore(
  { providedIn: 'root' },
  withState<ChatState>({
    conversations: [], messages: [], activeConversationId: null,
    hasMore: false, loading: false, error: null
  }),

  withComputed(store => ({
    activeConversation: computed(() =>
      store.conversations().find(c => c.id === store.activeConversationId()) ?? null)
  })),

  withMethods((store, api = inject(ChatApiService)) => ({
    async loadConversations() {
      try {
        const conversations = await firstValueFrom(api.getConversations());
        patchState(store, { conversations, error: null });
      } catch {
        patchState(store, { error: 'Could not load conversations.' });
      }
    },

    async openConversation(id: string) {
      patchState(store, { activeConversationId: id, messages: [], hasMore: false, loading: true, error: null });
      try {
        const messages = await firstValueFrom(api.getMessages(id));
        // Ignore a slow response if the user already switched to another conversation.
        if (store.activeConversationId() !== id) return;
        patchState(store, {
          messages,
          hasMore: messages.length >= PAGE,
          conversations: store.conversations().map(c => c.id === id ? { ...c, unreadCount: 0 } : c)
        });
      } catch {
        patchState(store, { error: 'Could not load messages.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async loadOlder() {
      const id = store.activeConversationId();
      const first = store.messages()[0];
      if (!id || !first || !store.hasMore()) return;
      try {
        const older = await firstValueFrom(api.getMessages(id, first.sentAt));
        if (store.activeConversationId() !== id) return;
        patchState(store, { messages: [...older, ...store.messages()], hasMore: older.length >= PAGE });
      } catch {
        patchState(store, { error: 'Could not load older messages.' });
      }
    },

    addMessage(message: ChatMessage, myId: string) {
      const isActive = message.conversationId === store.activeConversationId();
      const incoming = message.senderId !== myId;

      if (isActive && !store.messages().some(m => m.id === message.id))
        patchState(store, { messages: [...store.messages(), message] });

      patchState(store, {
        conversations: store.conversations()
          .map(c => c.id !== message.conversationId ? c : {
            ...c,
            lastMessage: message.content,
            lastAt: message.sentAt,
            // Only incoming messages in a conversation that isn't open count as unread.
            unreadCount: incoming && !isActive ? c.unreadCount + 1 : c.unreadCount
          })
          .sort(byRecent)
      });
    },

    applyReceipt(r: ReadReceipt) {
      if (r.conversationId !== store.activeConversationId()) return;
      const ids = new Set(r.messageIds);
      patchState(store, {
        messages: store.messages().map(m =>
          ids.has(m.id) && !m.readBy.includes(r.userId) ? { ...m, readBy: [...m.readBy, r.userId] } : m)
      });
    },

    upsertConversation(conversation: ChatConversation) {
      const exists = store.conversations().some(c => c.id === conversation.id);
      patchState(store, {
        conversations: (exists
          ? store.conversations().map(c => c.id === conversation.id
              ? { ...conversation, unreadCount: c.id === store.activeConversationId() ? 0 : conversation.unreadCount }
              : c)
          : [conversation, ...store.conversations()]).sort(byRecent)
      });
    },

    setError(error: string | null) { patchState(store, { error }); }
  }))
);
