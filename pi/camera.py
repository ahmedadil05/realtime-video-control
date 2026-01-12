import cv2

cap = cv2.VideoCapture(0)

while True:
    ret, frame = cap.read()
    if not ret:
        break

    # Resize early (critical for latency)
    frame = cv2.resize(frame, (640, 480))

    # Encode to JPEG
    success, encoded = cv2.imencode(
        ".jpg",
        frame,
        [cv2.IMWRITE_JPEG_QUALITY, 60]
    )

    if not success:
        continue

    payload = encoded.tobytes()
    print("Encoded size:", len(payload))

    # TEMP exit
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
