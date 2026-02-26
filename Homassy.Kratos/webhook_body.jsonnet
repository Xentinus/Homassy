// Kratos HTTP courier Jsonnet body template.
// Transforms the Kratos httpDataModel into the JSON payload
// sent to the Homassy Email microservice at POST /kratos/webhook.
//
// ctx fields (from Kratos courier/http_channel.go httpDataModel):
//   ctx.recipient       - destination email address
//   ctx.subject         - rendered email subject
//   ctx.body            - rendered email body
//   ctx.template_type   - e.g. "login_code_valid", "registration_code_valid"
//   ctx.template_data   - template model (varies by type, see below)
//   ctx.message_type    - "email" or "sms"
//
// For registration_code_valid, template_data is RegistrationCodeValidModel:
//   { to, traits, registration_code, request_url, transient_payload }
// Note: traits is top-level in template_data, NOT nested under identity.

function(ctx) {
  to: ctx.recipient,
  template_type: ctx.template_type,
  template_data: {
    to: ctx.recipient,
    login_code:
      if "template_data" in ctx && "login_code" in ctx.template_data
      then ctx.template_data.login_code
      else null,
    registration_code:
      if "template_data" in ctx && "registration_code" in ctx.template_data
      then ctx.template_data.registration_code
      else null,
    verification_code:
      if "template_data" in ctx && "verification_code" in ctx.template_data
      then ctx.template_data.verification_code
      else null,
    recovery_code:
      if "template_data" in ctx && "recovery_code" in ctx.template_data
      then ctx.template_data.recovery_code
      else null,
    identity: {
      id:
        if "template_data" in ctx && "identity" in ctx.template_data
        then ctx.template_data.identity.id
        else "",
      traits: {
        email: ctx.recipient,
        name:
          if "template_data" in ctx && "traits" in ctx.template_data && "name" in ctx.template_data.traits
          then ctx.template_data.traits.name
          else if "template_data" in ctx && "identity" in ctx.template_data && "traits" in ctx.template_data.identity && "name" in ctx.template_data.identity.traits
          then ctx.template_data.identity.traits.name
          else null,
        display_name:
          if "template_data" in ctx && "traits" in ctx.template_data && "display_name" in ctx.template_data.traits
          then ctx.template_data.traits.display_name
          else if "template_data" in ctx && "identity" in ctx.template_data && "traits" in ctx.template_data.identity && "display_name" in ctx.template_data.identity.traits
          then ctx.template_data.identity.traits.display_name
          else null,
        default_language:
          if "template_data" in ctx && "traits" in ctx.template_data && "default_language" in ctx.template_data.traits
          then ctx.template_data.traits.default_language
          else if "template_data" in ctx && "identity" in ctx.template_data && "traits" in ctx.template_data.identity && "default_language" in ctx.template_data.identity.traits
          then ctx.template_data.identity.traits.default_language
          else null,
      },
    },
  },
}
