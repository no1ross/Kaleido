import { enabledItemIds, holds } from './information-conditions';
import { InformationCondition, InformationItem, InformationItemType } from './information-request';

const question = (id: string, type: InformationItemType, enableWhen: InformationCondition[] = []): InformationItem =>
    ({ id, text: id, type, repeats: false, options: [], items: [], enableWhen });

const when = (q: string, operator: InformationCondition['operator'], answer?: string): InformationCondition =>
    ({ question: q, operator, answer });

describe('holds', () => {
    it.each([
        ['choice', 'equals', 'Knee', ['Knee'], true],
        ['choice', 'notEquals', 'Spine', ['Knee'], true],
        ['choice', 'notEquals', 'Spine', ['Spine'], false],
        ['boolean', 'equals', 'true', ['True'], true],
        ['wholeNumber', 'greaterThan', '5', ['6'], true],
        ['wholeNumber', 'greaterThan', '5', ['5'], false],
        ['number', 'lessThanOrEqual', '2.5', ['2.50'], true],
        ['date', 'lessThan', '2024-05-01', ['2024-04-30'], true],
        ['wholeNumber', 'lessThan', '5', ['several'], false],
        ['choice', 'equals', 'Knee', ['Spine', 'Knee'], true],
        ['choice', 'notEquals', 'Knee', ['Spine', 'Knee'], false]
    ] as const)('%s %s %s with %j is %s', (type, operator, expected, answers, result) => {
        expect(holds(when('q', operator, expected), question('q', type), answers)).toBe(result);
    });

    it('holds only "exists = false" for an unanswered question', () => {
        expect(holds(when('q', 'exists', 'false'), question('q', 'text'), [])).toBe(true);
        expect(holds(when('q', 'exists', 'true'), question('q', 'text'), [])).toBe(false);
        expect(holds(when('q', 'notEquals', 'x'), question('q', 'text'), [])).toBe(false);
        expect(holds(when('q', 'exists', 'true'), question('q', 'text'), ['x'])).toBe(true);
    });
});

describe('enabledItemIds', () => {
    // MRI: laterality only when the body part is not the spine; weeks only when therapy was tried.
    const mri: InformationItem[] = [
        question('bodyPart', 'choice'),
        question('laterality', 'choice', [when('bodyPart', 'notEquals', 'Spine')]),
        question('therapy', 'boolean'),
        {
            ...question('therapyDetails', 'group', [when('therapy', 'equals', 'true')]),
            items: [question('weeks', 'wholeNumber')]
        },
        {
            ...question('either', 'text', [when('bodyPart', 'equals', 'Knee'), when('therapy', 'equals', 'true')]),
            enableBehavior: 'any'
        }
    ];

    const sorted = (ids: Set<string>) => [...ids].sort();

    it('follows the answers, and a group disables its items', () => {
        expect(sorted(enabledItemIds(mri, { bodyPart: ['Spine'], therapy: ['false'] }))).toEqual(['bodyPart', 'therapy']);
        expect(sorted(enabledItemIds(mri, { bodyPart: ['Knee'], therapy: ['true'] })))
            .toEqual(['bodyPart', 'either', 'laterality', 'therapy', 'therapyDetails', 'weeks']);
    });

    it('needs one condition with "any"', () => {
        expect(enabledItemIds(mri, { bodyPart: ['Spine'], therapy: ['true'] }).has('either')).toBe(true);
        expect(enabledItemIds(mri, { bodyPart: ['Spine'], therapy: ['false'] }).has('either')).toBe(false);
    });

    it('ignores the answer of a question that does not apply', () => {
        const chain = [
            question('a', 'boolean'),
            question('b', 'text', [when('a', 'equals', 'true')]),
            question('c', 'text', [when('b', 'exists', 'true')])
        ];

        expect(enabledItemIds(chain, { a: ['false'], b: ['stale'] }).has('c')).toBe(false);
        expect(enabledItemIds(chain, { a: ['true'], b: ['fresh'] }).has('c')).toBe(true);
    });
});
