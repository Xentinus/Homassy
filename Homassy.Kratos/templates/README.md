# Kratos Email Templates

This directory contains localized email templates for Ory Kratos.

## Structure

```
templates/
├── verification/
│   └── valid/
│       ├── email.body.html.hu.gotmpl
│       ├── email.body.html.de.gotmpl
│       ├── email.body.html.en.gotmpl
│       ├── email.subject.hu.gotmpl
│       ├── email.subject.de.gotmpl
│       └── email.subject.en.gotmpl
├── recovery/
│   └── valid/
│       ├── email.body.html.*.gotmpl
│       └── email.subject.*.gotmpl
└── login/
    └── code/
        └── valid/
            ├── email.body.html.*.gotmpl
            └── email.subject.*.gotmpl
```

## Language Selection

Kratos selects the appropriate template based on:
1. User's `default_language` trait (if set)
2. Accept-Language header
3. Falls back to English (en)

## Template Variables

Available in templates:
- `{{ .To }}` - Recipient email
- `{{ .VerificationCode }}` - The verification/login code
- `{{ .VerificationURL }}` - Full verification URL
- `{{ .RecoveryCode }}` - Recovery code
- `{{ .RecoveryURL }}` - Full recovery URL
- `{{ .Identity }}` - Full identity object with traits
- `{{ .Identity.traits.name }}` - User's name
- `{{ .Identity.traits.display_name }}` - User's display name

## Customization

Templates use Go's html/template syntax. See:
https://www.ory.sh/docs/kratos/emails-sms/custom-email-templates
