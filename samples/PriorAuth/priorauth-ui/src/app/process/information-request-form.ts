import { Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
    InformationItem,
    InformationRequest,
    InformationResponseItem
} from '../kaleido/models/information-request';

interface RenderedItem {
    item: InformationItem;
    depth: number;
}

/**
 * Renders any Kaleido information request and emits the answers. Question and display text is
 * shown verbatim; every question must be answered (the service decides what to ask, including
 * any "none of the above" option).
 */
@Component({
    selector: 'priorauth-information-request-form',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './information-request-form.html',
    styleUrl: './information-request-form.scss'
})
export class InformationRequestForm {
    readonly request = input.required<InformationRequest>();
    readonly submitLabel = input('Submit');
    readonly isSubmitting = input(false);
    readonly submitted = output<InformationResponseItem[]>();

    /** Single answers by item id; repeating choices keep every selected value. */
    readonly answers = signal<Record<string, string[]>>({});

    readonly items =
        computed(() => this.flatten(this.request().items, 0));

    readonly questions =
        computed(() => this.items()
            .map(x => x.item)
            .filter(item => item.type !== 'display' && item.type !== 'group'));

    readonly isComplete =
        computed(() => this.questions().every(question =>
            (this.answers()[question.id] ?? []).some(value => value.trim().length > 0)));

    constructor() {
        // A new request (a new round) starts with no answers.
        effect(() => {
            this.request();
            this.answers.set({});
        });
    }

    valueOf(item: InformationItem): string {
        return this.answers()[item.id]?.[0] ?? '';
    }

    isSelected(item: InformationItem, value: string): boolean {
        return (this.answers()[item.id] ?? []).includes(value);
    }

    setValue(item: InformationItem, value: string): void {
        this.answers.update(answers => ({ ...answers, [item.id]: [value] }));
    }

    toggle(item: InformationItem, value: string, selected: boolean): void {
        this.answers.update(answers => {
            const current = (answers[item.id] ?? []).filter(x => x !== value);
            return { ...answers, [item.id]: selected ? [...current, value] : current };
        });
    }

    submit(): void {
        if (!this.isComplete() || this.isSubmitting()) {
            return;
        }

        this.submitted.emit(
            this.questions().map(question => ({
                itemId: question.id,
                answers: (this.answers()[question.id] ?? [])
                    .filter(value => value.trim().length > 0)
                    .map(value => ({ value: this.normalize(question, value) }))
            })));
    }

    // Kaleido expects invariant text: ISO dates/date-times, true/false, numbers with '.'.
    private normalize(item: InformationItem, value: string): string {
        return item.type === 'dateTime' && value.length === 16
            ? new Date(value).toISOString()
            : value.trim();
    }

    private flatten(items: InformationItem[], depth: number): RenderedItem[] {
        return items.flatMap(item => [
            { item, depth },
            ...this.flatten(item.items ?? [], depth + 1)
        ]);
    }
}
