# Razorpay Payment Flow — Frontend Integration Guide

Base URL: `https://app.dikshitech.com/sjdigichit/API`

All endpoints (except webhook) require JWT Bearer token in the header:
```
Authorization: Bearer <token>
```

---

## Full Flow Overview

```
App
 │
 ├── 1. POST /api/Payment/create-order        ← Get Razorpay order_id
 │
 ├── 2. POST /api/Payment/save-pending        ← Save full payload BEFORE opening checkout
 │
 ├── 3. Razorpay Checkout opens               ← SDK handles payment
 │
 ├── 4. POST /api/Payment/verify-payment      ← Verify signature (on success callback)
 │
 ├── 5. POST /api/SchemeDetails/InsertChitScheme  ← Insert business record
 │
 └── 6. POST /api/Payment/record              ← Save payment audit log
```

Webhook runs in background automatically — no frontend action needed.

---

## API 1 — Create Order

**Endpoint:** `POST /api/Payment/create-order`

**When to call:** User clicks "Pay Now" button.

**Request:**
```json
{
  "amount": "1000"
}
```

**Response:**
```json
{
  "orderId": "order_XXXXXXXXXX",
  "amount": 1000,
  "currency": "INR",
  "keyId": "rzp_live_XXXXXXXXXX"
}
```

**React Native example:**
```javascript
const createOrder = async (amount) => {
  const response = await fetch(`${BASE_URL}/api/Payment/create-order`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ amount: amount.toString() }),
  });
  const data = await response.json();
  // data.orderId, data.keyId save panni next step-la use pannu
  return data;
};
```

---

## API 2 — Save Pending

**Endpoint:** `POST /api/Payment/save-pending`

**When to call:** AFTER create-order, BEFORE opening Razorpay checkout.
This is the safety net — app close aana, network cut aana, webhook idha use pannum.

**Request:**
```json
{
  "razorpayOrderId": "order_XXXXXXXXXX",
  "userId": "CUS001",
  "chitPayload": {
    "schemeDetails": [
      {
        "cusCode": "CUS001",
        "schemeCode": "SCH001",
        "amount": "1000.00",
        "totalAmt": "1000.00",
        "compCode": "001",
        "fDUE": "1",
        "weight": null,
        "fbwt": null,
        "fbamt": null,
        "fbfinalamt": null,
        "finalwt": null,
        "fGRATE": null
      }
    ],
    "razorpayPaymentId": null,
    "hasReferral": 0,
    "userId": "CUS001",
    "referrerId": null
  }
}
```

**Response:**
```json
{
  "message": "Pending payment saved."
}
```

**React Native example:**
```javascript
const savePending = async (orderId, userId, chitPayload) => {
  const response = await fetch(`${BASE_URL}/api/Payment/save-pending`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({
      razorpayOrderId: orderId,
      userId: userId,
      chitPayload: chitPayload,
    }),
  });
  return await response.json();
};
```

---

## API 3 — Razorpay Checkout (SDK)

**When to call:** After save-pending success.

**React Native example (react-native-razorpay):**
```javascript
import RazorpayCheckout from 'react-native-razorpay';

const openCheckout = (orderData, userDetails) => {
  const options = {
    description: 'Chit Scheme Payment',
    image: 'https://your-logo-url.png',
    currency: 'INR',
    key: orderData.keyId,           // from create-order response
    amount: orderData.amount * 100, // paise
    order_id: orderData.orderId,    // from create-order response
    name: 'Saroj Jewellers',
    prefill: {
      email: userDetails.email,
      contact: userDetails.phone,   // mandatory — webhook uses this as fallback
      name: userDetails.name,
    },
    theme: { color: '#F37254' },
  };

  RazorpayCheckout.open(options)
    .then((paymentResponse) => {
      // Payment success — paymentResponse has:
      // razorpay_payment_id
      // razorpay_order_id
      // razorpay_signature
      handlePaymentSuccess(paymentResponse);
    })
    .catch((error) => {
      // User cancelled or payment failed
      handlePaymentFailure(error);
    });
};
```

**Important:** `prefill.contact` — phone number correct-a pannu. Webhook fallback-la itha use pannum.

---

## API 4 — Verify Payment

**Endpoint:** `POST /api/Payment/verify-payment`

**When to call:** Inside Razorpay success callback (`.then()`), immediately.

> **Important:** `save-pending` must be called before opening checkout — `verify-payment` validates the order_id against `PendingPayments` table. If `save-pending` was skipped, this will return `400 Order not found`.

**Request:**
```json
{
  "razorpay_payment_id": "pay_XXXXXXXXXX",
  "razorpay_order_id": "order_XXXXXXXXXX",
  "razorpay_signature": "abc123def456..."
}
```

**Response (success):**
```json
{
  "status": "success",
  "message": "Payment verified successfully"
}
```

**Response (failure — signature mismatch):**
```json
{
  "status": "failed",
  "message": "Payment verification failed — signature mismatch."
}
```

**Response (failure — order not in DB):**
```json
{
  "status": "failed",
  "message": "Order not found or already processed."
}
```

**React Native example:**
```javascript
const verifyPayment = async (paymentResponse) => {
  const response = await fetch(`${BASE_URL}/api/Payment/verify-payment`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({
      razorpay_payment_id: paymentResponse.razorpay_payment_id,
      razorpay_order_id:   paymentResponse.razorpay_order_id,
      razorpay_signature:  paymentResponse.razorpay_signature,
    }),
  });
  const data = await response.json();
  if (data.status !== 'success') {
    throw new Error('Signature verification failed');
  }
  return data;
};
```

---

## API 5 — Insert Chit Scheme (Business Record)

**Endpoint:** `POST /api/SchemeDetails/InsertChitScheme`

**When to call:** After verify-payment returns `status: "success"`.

**Request:**
```json
{
  "schemeDetails": [
    {
      "cusCode": "CUS001",
      "schemeCode": "SCH001",
      "amount": "1000.00",
      "totalAmt": "1000.00",
      "compCode": "001",
      "fDUE": "1",
      "weight": null,
      "fbwt": null,
      "fbamt": null,
      "fbfinalamt": null,
      "finalwt": null,
      "fGRATE": null
    }
  ],
  "razorpayPaymentId": "pay_XXXXXXXXXX",
  "hasReferral": 0,
  "userId": "CUS001",
  "referrerId": null
}
```

**Response (success):**
```json
{
  "Message": "Insert successful.",
  "VoucherNo": "CT0001234"
}
```

**Response (already inserted — idempotent):**
```json
{
  "Message": "Already inserted.",
  "VoucherNo": "CT0001234"
}
```

> **Note:** Response keys are PascalCase (`Message`, `VoucherNo`) — use exactly as shown.

**React Native example:**
```javascript
const insertChitScheme = async (chitPayload, paymentId) => {
  const body = {
    ...chitPayload,
    razorpayPaymentId: paymentId,
  };
  const response = await fetch(`${BASE_URL}/api/SchemeDetails/InsertChitScheme`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify(body),
  });
  return await response.json();
};
```

---

## API 6 — Record Payment (Audit Log)

**Endpoint:** `POST /api/Payment/record`

**When to call:** After InsertChitScheme success. This is audit/history only.

**Request:**
```json
{
  "userId": "CUS001",
  "razorpayOrderId": "order_XXXXXXXXXX",
  "razorpayPaymentId": "pay_XXXXXXXXXX",
  "razorpaySignature": "abc123def456...",
  "amount": 1000.00,
  "currency": "INR",
  "status": "success",
  "description": "Chit Scheme Payment",
  "email": "user@email.com",
  "contact": "9876543210",
  "fpaymentType": "Y"
}
```

**Response:**
```json
{
  "status": "success",
  "message": "Payment record inserted successfully"
}
```

---

## Complete Flow — React Native

```javascript
const handlePayNow = async () => {
  try {
    // ── Step 1: Create Order ──────────────────────────────────
    const orderData = await createOrder(schemeAmount);

    // ── Step 2: Save Pending (BEFORE checkout opens) ──────────
    await savePending(orderData.orderId, userId, chitPayload);

    // ── Step 3: Open Razorpay Checkout ────────────────────────
    RazorpayCheckout.open({
      key:      orderData.keyId,
      amount:   orderData.amount * 100,
      currency: 'INR',
      order_id: orderData.orderId,
      name:     'Saroj Jewellers',
      prefill: {
        contact: userPhone,   // important for webhook fallback
        email:   userEmail,
        name:    userName,
      },
    })
    .then(async (paymentResponse) => {
      // ── Step 4: Verify Signature ────────────────────────────
      await verifyPayment(paymentResponse);

      // ── Step 5: Insert Business Record ─────────────────────
      const insertResult = await insertChitScheme(
        chitPayload,
        paymentResponse.razorpay_payment_id
      );

      // ── Step 6: Audit Log ───────────────────────────────────
      await recordPayment({
        userId,
        razorpayOrderId:   paymentResponse.razorpay_order_id,
        razorpayPaymentId: paymentResponse.razorpay_payment_id,
        razorpaySignature: paymentResponse.razorpay_signature,
        amount:            schemeAmount,
        currency:          'INR',
        status:            'success',
        contact:           userPhone,
        fpaymentType:      'Y',
      });

      // ── Success ─────────────────────────────────────────────
      navigation.navigate('PaymentSuccess', {
        voucherNo: insertResult.VoucherNo,  // PascalCase — matches API response
      });
    })
    .catch((error) => {
      // User cancelled or payment failed
      // Webhook will handle if payment actually went through
      showError('Payment failed or cancelled');
    });

  } catch (error) {
    showError(error.message);
  }
};
```

---

## Error Handling

| Scenario | What happens |
|---|---|
| App closed after GPay payment | Webhook runs automatically, inserts from PendingPayments |
| verify-payment fails | Don't call InsertChitScheme, show error |
| InsertChitScheme fails | Webhook already handles it (idempotent) |
| Network cut mid-payment | Webhook safety net triggers |
| Duplicate webhook delivery | `RazorpayWebhookEvents` UNIQUE constraint blocks it |

---

## What NOT to do

- `verify-payment` skip panna koodadhu — always verify before insert
- `save-pending` skip panna koodadhu — webhook fallback idha depend pannum
- `InsertChitScheme` failure-a final-a treat panna koodadhu — webhook handles it
- `prefill.contact` blank-a vidaradhu — webhook phone fallback fail aagum
