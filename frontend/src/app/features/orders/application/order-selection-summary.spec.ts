import { summarizeOrderSelection } from './order-selection-summary';

describe('order-selection-summary', () => {
  it('returns zero MXN when no orders are selected', () => {
    expect(summarizeOrderSelection([])).toEqual({
      count: 0,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 0 }],
    });
  });

  it('uses the selected order total for one selected order', () => {
    expect(summarizeOrderSelection([{ total: 116, currencyCode: 'MXN' }])).toEqual({
      count: 1,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 116 }],
    });
  });

  it('sums multiple selected orders in the same currency', () => {
    expect(summarizeOrderSelection([
      { total: 116, currencyCode: 'MXN' },
      { total: 58, currencyCode: 'MXN' },
      { total: 0.335, currencyCode: 'MXN' },
    ])).toEqual({
      count: 3,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 174.34 }],
    });
  });

  it('updates the total when an order is removed from the selected set', () => {
    const selectedOrders = [
      { total: 116, currencyCode: 'MXN' },
      { total: 58, currencyCode: 'MXN' },
    ];

    expect(summarizeOrderSelection(selectedOrders.slice(0, 1))).toEqual({
      count: 1,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 116 }],
    });
  });

  it('summarizes a select-all result from the current selected orders', () => {
    expect(summarizeOrderSelection([
      { total: 100, currencyCode: 'MXN' },
      { total: 200, currencyCode: 'MXN' },
      { total: 300, currencyCode: 'MXN' },
    ])).toEqual({
      count: 3,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 600 }],
    });
  });

  it('groups totals by currency instead of mixing amounts', () => {
    expect(summarizeOrderSelection([
      { total: 8500, currencyCode: 'MXN' },
      { total: 320, currencyCode: 'usd' },
      { total: 1500, currencyCode: 'MXN' },
    ])).toEqual({
      count: 3,
      totalsByCurrency: [
        { currencyCode: 'MXN', amount: 10000 },
        { currencyCode: 'USD', amount: 320 },
      ],
    });
  });

  it('uses MXN when currency is not available', () => {
    expect(summarizeOrderSelection([
      { total: 100, currencyCode: null },
      { total: 50 },
    ])).toEqual({
      count: 2,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 150 }],
    });
  });

  it('treats null, undefined and non numeric totals as zero', () => {
    expect(summarizeOrderSelection([
      { total: null, currencyCode: 'MXN' },
      { total: undefined, currencyCode: 'MXN' },
      { total: 'not-a-number', currencyCode: 'MXN' },
      { total: Number.NaN, currencyCode: 'MXN' },
      { total: '25.25', currencyCode: 'MXN' },
    ])).toEqual({
      count: 5,
      totalsByCurrency: [{ currencyCode: 'MXN', amount: 25.25 }],
    });
  });
});
