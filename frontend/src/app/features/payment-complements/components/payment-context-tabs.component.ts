import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  QueryList,
  ViewChildren,
  input,
  output,
} from '@angular/core';

export interface PaymentContextTabItem {
  id: string;
  label: string;
}

@Component({
  selector: 'app-payment-context-tabs',
  template: `
    <nav class="tabs" role="tablist" [attr.aria-label]="ariaLabel()">
      @for (tab of tabs(); track tab.id; let index = $index) {
        <button
          #tabButton
          type="button"
          class="tab-button"
          [class.compact]="compact()"
          [class.active]="activeTab() === tab.id"
          role="tab"
          [attr.id]="tabButtonId(tab.id)"
          [attr.aria-selected]="activeTab() === tab.id"
          [attr.aria-controls]="tabPanelId(tab.id)"
          [attr.tabindex]="activeTab() === tab.id ? 0 : -1"
          (click)="select(tab.id)"
          (keydown)="handleKeydown($event, index)"
        >
          {{ tab.label }}
        </button>
      }
    </nav>
  `,
  styles: [
    `
      .tabs {
        display: flex;
        flex-wrap: wrap;
        gap: 0.65rem;
      }

      .tab-button {
        border: 1px solid #d8d1c2;
        background: #fff;
        color: #182533;
        border-radius: 999px;
        padding: 0.65rem 1rem;
        cursor: pointer;
        font: inherit;
        transition: background-color 120ms ease, border-color 120ms ease, color 120ms ease,
          box-shadow 120ms ease;
      }

      .tab-button.compact {
        padding: 0.45rem 0.8rem;
        font-size: 0.9rem;
      }

      .tab-button.active {
        background: #182533;
        color: #fff;
        border-color: #182533;
      }

      .tab-button:focus-visible {
        outline: none;
        box-shadow: 0 0 0 3px rgba(24, 37, 51, 0.18);
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentContextTabsComponent {
  @ViewChildren('tabButton')
  private readonly tabButtons?: QueryList<ElementRef<HTMLButtonElement>>;

  readonly tabs = input.required<PaymentContextTabItem[]>();
  readonly activeTab = input.required<string>();
  readonly ariaLabel = input('Navegacion del contexto');
  readonly idPrefix = input('payment-context');
  readonly compact = input(false);
  readonly tabChanged = output<string>();

  protected select(tabId: string): void {
    if (tabId === this.activeTab()) {
      return;
    }

    this.tabChanged.emit(tabId);
  }

  protected handleKeydown(event: KeyboardEvent, index: number): void {
    const tabs = this.tabs();
    if (!tabs.length) {
      return;
    }

    let nextIndex = index;

    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        nextIndex = (index + 1) % tabs.length;
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        nextIndex = (index - 1 + tabs.length) % tabs.length;
        break;
      case 'Home':
        nextIndex = 0;
        break;
      case 'End':
        nextIndex = tabs.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    this.focusAndSelect(nextIndex);
  }

  protected tabButtonId(tabId: string): string {
    return `${this.idPrefix()}-tab-${tabId}`;
  }

  protected tabPanelId(tabId: string): string {
    return `${this.idPrefix()}-panel-${tabId}`;
  }

  private focusAndSelect(index: number): void {
    const tab = this.tabs()[index];
    if (!tab) {
      return;
    }

    this.tabButtons?.get(index)?.nativeElement.focus();
    this.tabChanged.emit(tab.id);
  }
}
