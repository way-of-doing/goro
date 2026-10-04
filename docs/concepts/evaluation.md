# Evaluation

## NAME

evaluation - what each construct of a predicate evaluates to

## DESCRIPTION

This document defines what every construct of a [predicate](predicates.md) evaluates to, compactly and without examples; the predicate documentation explains the same rules at length. The two describe one semantics, and a disagreement between them is a defect. Until such a defect is corrected, tests follow this document. Identifiers are resolved as described under [built-in identifiers](../features/builtins/identifiers.md), and strings are prepared as described under [Normalization](normalization.md).

### Values and notation

```
value      = ABSENT | bag of one or more occurrences
occurrence = usable(datum) | unusable(source)
```

A boolean is written `T`, `F` or `U`, for a bag holding one usable true, one usable false, or one unusable occurrence. An operator's unusable result has no source.

Evaluation is against one file at a time. Each operand of a comparison, range or regex operator has a _quantifier_, `∀` if the outermost quantifier modifier written on it is `ALL` and `∃` otherwise, and each such operator has a _mode_, literal if any of its operands carries `LITERALLY` and normalized otherwise. `report(o)` records the source of an unusable occurrence `o`, if it has one, for the file being evaluated.

### Static judgements

Every expression has a type and is or is not definite, both decided when the predicate is read. Here, an _operator_ is any comparison, range, regex, state test or logical operator.

| Expression                | Type        | Definite  |
|---------------------------|-------------|-----------|
| literal                   | as written  | yes
| identifier                | as declared | as declared
| `COUNT(e)`                | number      | yes
| `NUMBER(e)`               | number      | when `e` is
| `STRING(e)`               | string      | when `e` is
| `FALLBACK(e, d)`          | that of `e` | when `e` is
| any operator              | boolean     | yes
| `(e)`                     | that of `e` | when `e` is
| a modifier applied to `e` | that of `e` | when `e` is

The operands each construct accepts are given in the predicate documentation, and under [Constraints not expressed by the grammar](predicates.md#constraints-not-expressed-by-the-grammar).

### Expressions

```
eval(literal)          = bag{ usable(its value) }
eval(identifier)       = as resolved by identifiers.md; an unusable occurrence has the identifier as its source
eval((e))              = eval(e)
eval(modifier(e))      = eval(e)              -- the modifier is read by the operator, not by eval

eval(COUNT(e))         = bag{ usable(0) }               if eval(e) is ABSENT
                       = bag{ usable(number of occurrences of eval(e)) } otherwise

eval(NUMBER(e))        = ABSENT                         if eval(e) is ABSENT
                       = eval(e) with each occurrence o replaced by
                           o                            if o is unusable
                           usable(the number o denotes) if o converts
                           unusable(this call)          otherwise

eval(STRING(e))        = ABSENT                         if eval(e) is ABSENT
                       = eval(e) with each usable(d) replaced by usable(the canonical text of d)

eval(FALLBACK(e, d))   = bag{ usable(d) }               if eval(e) is ABSENT
                       = eval(e) with each unusable occurrence replaced by usable(d)
```

### Comparison, range and regex operators

For a comparison, the operands are its two sides; for `BETWEEN`, the operand is its left side, the range being part of the test; for `~=`, the operand is the subject, the pattern being part of the test.

```
eval(op(e1 … en)):
    vi = eval(ei) for each operand
    if any vi is ABSENT: return F
    arrange the operands so that those quantified ∀ come first, each group in written order
    return over(1, nothing chosen)

over(k, chosen):
    if k > n: return test(chosen)
    outcomes = [ over(k+1, chosen + o) for every occurrence o of vk ]      -- every one, always
    return quantify(quantifier of operand k, outcomes)

test(o1 … on):
    if any oj is unusable: report each such oj; return U
    prepare each string datum, and the endpoints of a range, per normalization.md in the operator's mode;
        a pattern is not prepared
    return T if the operator's condition holds of the data, F otherwise

quantify(∃, outcomes) = T if any is T, else U if any is U, else F
quantify(∀, outcomes) = F if any is F, else U if any is U, else T
```

Because every combination is evaluated, an operator that is evaluated and has no absent operand reports every unusable occurrence among its operands.

### State tests and logical operators

```
eval(e IS ABSENT)    = T if eval(e) is ABSENT, else F
eval(e IS USABLE)    = F if eval(e) is ABSENT, else quantify(Q, [ T if o is usable else F   for each o ])
eval(e IS UNUSABLE)  = F if eval(e) is ABSENT, else quantify(Q, [ T if o is unusable else F for each o ])
                       -- Q is the quantifier written on e; nothing is reported

eval(NOT a)          = T if eval(a) is F, F if it is T, U if it is U
eval(a AND b)        = F if eval(a) is F, and b is not evaluated
                     = F if eval(b) is F, else U if either is U, else T
eval(a OR b)         = T if eval(a) is T, and b is not evaluated
                     = T if eval(b) is T, else U if either is U, else F
```

### The result and its reports

The result of a predicate for a file is `eval` of the whole predicate, one of `T`, `F` and `U`; what a command does with `U` is the command's to say. Each distinct source reported while evaluating it produces one warning for that file, sources being compared as described under [Deduplication](predicates.md#deduplication).

## SEE ALSO

[Predicates](predicates.md), [Normalization](normalization.md), [built-in identifiers](../features/builtins/identifiers.md)
