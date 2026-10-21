// Which items of a Kaleido information request apply, given the answers so far (enableWhen, ADR 0024).
// The same rules Kaleido validates with, so the form only asks - and only sends - what applies:
// - an item applies when its conditions hold (all, or any) and its group applies;
// - a condition tests an earlier question; the answer of a question that does not apply counts as none;
// - with no answer only "exists = false" holds; with several answers, equals and the ordering operators
//   hold when any answer matches, notEquals when none does;
// - numbers, dates and date-times compare by value, everything else by exact text.

import {
    InformationCondition,
    InformationItem,
    InformationItemType
} from './information-request';

export type Answers = Readonly<Record<string, readonly string[]>>;

/** The ids of the items (questions, groups and display text) that apply given the answers. */
export function enabledItemIds(items: readonly InformationItem[], answers: Answers): Set<string> {
    const questions = new Map<string, InformationItem>();
    collectQuestions(items, questions);

    const enabled = new Set<string>();
    evaluate(items, true, questions, answers, enabled);
    return enabled;
}

/** Whether a condition holds for the (applying) answers of its question. */
export function holds(condition: InformationCondition, question: InformationItem, answers: readonly string[]): boolean {
    const given = answers.filter(value => value.trim().length > 0);

    if (condition.operator === 'exists') {
        return (given.length > 0) === ((condition.answer ?? '').toLowerCase() === 'true');
    }

    const expected = condition.answer;

    if (given.length === 0 || expected === undefined) {
        return false;
    }

    const compare = (value: string) => compareAnswers(question.type, value, expected);

    switch (condition.operator) {
        case 'equals': return given.some(value => compare(value) === 0);
        case 'notEquals': return given.every(value => compare(value) !== 0);
        case 'greaterThan': return given.some(value => (compare(value) ?? 0) > 0);
        case 'lessThan': return given.some(value => (compare(value) ?? 0) < 0);
        case 'greaterThanOrEqual': return given.some(value => { const c = compare(value); return c !== undefined && c >= 0; });
        case 'lessThanOrEqual': return given.some(value => { const c = compare(value); return c !== undefined && c <= 0; });
        default: return false;
    }
}

function evaluate(
    items: readonly InformationItem[],
    parentEnabled: boolean,
    questions: Map<string, InformationItem>,
    answers: Answers,
    enabled: Set<string>
): void {
    for (const item of items) {
        const conditions = item.enableWhen ?? [];
        const conditionHolds = (condition: InformationCondition) => {
            const question = questions.get(condition.question);
            return question !== undefined
                && holds(condition, question, enabled.has(question.id) ? answers[question.id] ?? [] : []);
        };

        const isEnabled = parentEnabled && (
            conditions.length === 0
            || (item.enableBehavior === 'any' ? conditions.some(conditionHolds) : conditions.every(conditionHolds)));

        if (isEnabled) {
            enabled.add(item.id);
        }

        evaluate(item.items ?? [], isEnabled, questions, answers, enabled);
    }
}

function collectQuestions(items: readonly InformationItem[], questions: Map<string, InformationItem>): void {
    for (const item of items) {
        if (item.type !== 'group' && item.type !== 'display') {
            questions.set(item.id, item);
        }

        collectQuestions(item.items ?? [], questions);
    }
}

// Ordering for numbers, dates and date-times; 0 or 1 (equal or not) for the other types; undefined
// when either side cannot be read as the type (then no comparison holds).
function compareAnswers(type: InformationItemType, value: string, expected: string): number | undefined {
    switch (type) {
        case 'wholeNumber':
        case 'number':
            return orderOf(Number(value.trim()), Number(expected.trim()), value, expected);
        case 'date':
        case 'dateTime':
            return orderOf(Date.parse(value.trim()), Date.parse(expected.trim()), value, expected);
        case 'boolean':
            return isBoolean(value) && isBoolean(expected)
                ? (value.trim().toLowerCase() === expected.trim().toLowerCase() ? 0 : 1)
                : undefined;
        default:
            return value.trim() === expected.trim() ? 0 : 1;
    }
}

function orderOf(a: number, b: number, rawA: string, rawB: string): number | undefined {
    return rawA.trim() === '' || rawB.trim() === '' || Number.isNaN(a) || Number.isNaN(b)
        ? undefined
        : Math.sign(a - b);
}

function isBoolean(value: string): boolean {
    return ['true', 'false'].includes(value.trim().toLowerCase());
}
