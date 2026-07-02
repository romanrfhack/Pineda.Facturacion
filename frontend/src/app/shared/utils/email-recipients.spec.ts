import {
  findInvalidEmailRecipients,
  formatEmailRecipientsInput,
  parseEmailRecipients,
  splitEmailRecipients,
} from './email-recipients';

describe('email recipients helpers', () => {
  it('splits recipients separated by semicolon', () => {
    expect(splitEmailRecipients('a@x.com; b@y.com')).toEqual([
      'a@x.com',
      'b@y.com',
    ]);
  });

  it('splits recipients separated by comma', () => {
    expect(splitEmailRecipients('a@x.com, b@y.com')).toEqual([
      'a@x.com',
      'b@y.com',
    ]);
  });

  it('splits recipients separated by line breaks', () => {
    expect(splitEmailRecipients('a@x.com\nb@y.com\r\nc@z.com')).toEqual([
      'a@x.com',
      'b@y.com',
      'c@z.com',
    ]);
  });

  it('parses only valid recipients after the caller has already validated the input', () => {
    expect(parseEmailRecipients('a@x.com; b@y.com,\nc@z.com')).toEqual([
      'a@x.com',
      'b@y.com',
      'c@z.com',
    ]);
  });

  it('finds invalid recipients without dropping the raw token', () => {
    expect(findInvalidEmailRecipients('a@x.com; invalido\notro-invalido')).toEqual([
      'invalido',
      'otro-invalido',
    ]);
  });

  it('does not use parseEmailRecipients as a substitute for validation', () => {
    expect(parseEmailRecipients('a@x.com; invalido')).toEqual(['a@x.com']);
    expect(findInvalidEmailRecipients('a@x.com; invalido')).toEqual(['invalido']);
  });

  it('formats valid recipients using the canonical semicolon separator', () => {
    expect(formatEmailRecipientsInput('a@x.com,b@y.com')).toBe('a@x.com; b@y.com');
  });
});
