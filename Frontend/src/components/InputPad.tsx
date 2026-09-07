import {
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type WheelEvent as ReactWheelEvent,
} from "react";
import { Box, Button, HStack, Input, Text, VStack } from "@chakra-ui/react";
import { useInputSocket } from "../services/useInputSocket";

/**
 * General-purpose remote input: a touchpad whose pointer drags move the host mouse
 * (tap = left click, wheel = scroll), plus a short text box whose keystrokes are replayed
 * as letters/keys on the host. Uses the WebSocket channel for low latency.
 */
export default function InputPad({ password }: { password: string }) {
  const { ready, error, moveMouse, click, scroll, typeText, pressKey } =
    useInputSocket(password || null);

  const [text, setText] = useState("");
  const last = useRef({ x: 0, y: 0 });
  const dragging = useRef(false);
  const moved = useRef(false);
  const pending = useRef({ dx: 0, dy: 0, raf: 0 as number | 0 });

  // Multiplier applied to the pointer delta before it's streamed to the host.
  const SENSITIVITY = 1.5;

  const flush = () => {
    const p = pending.current;
    p.raf = 0;
    if (p.dx !== 0 || p.dy !== 0) {
      moveMouse(Math.round(p.dx * SENSITIVITY), Math.round(p.dy * SENSITIVITY));
      p.dx = 0;
      p.dy = 0;
    }
  };

  const onPointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
    dragging.current = true;
    moved.current = false;
    last.current = { x: e.clientX, y: e.clientY };
    e.currentTarget.setPointerCapture(e.pointerId);
  };

  const onPointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!dragging.current) return;
    const dx = e.clientX - last.current.x;
    const dy = e.clientY - last.current.y;
    last.current = { x: e.clientX, y: e.clientY };
    if (dx !== 0 || dy !== 0) moved.current = true;
    const p = pending.current;
    p.dx += dx;
    p.dy += dy;
    if (!p.raf) p.raf = window.requestAnimationFrame(flush);
  };

  const onPointerUp = () => {
    dragging.current = false;
    if (!moved.current) click("left"); // a tap is a left click
    flush();
  };

  const onWheel = (e: ReactWheelEvent<HTMLDivElement>) => {
    scroll(e.deltaY > 0 ? -1 : 1);
  };

  return (
    <HStack gap={3} marginTop={3}>
      {/* Mouse cluster: touchpad with R/M buttons flush beside it, filling its height. */}
      <HStack spacing={0} align="flex-start">
        <Box
          data-testid="touchpad"
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onWheel={onWheel}
          style={{
            touchAction: "none",
            userSelect: "none",
            cursor: "crosshair",
          }}
          width="180px"
          height="120px"
          bg="gray.800"
          border="1px solid"
          borderColor="gray.600"
          borderRadius="md"
          borderTopRightRadius={0}
          borderBottomRightRadius={0}
        />
        <VStack
          width="24px"
          minWidth="24px"
          gap={"1px"}
          marginLeft={"4px"}
          spacing={0}
        >
          <Button
            height="60px"
            width="24px"
            minWidth="24px"
            borderTopLeftRadius={0}
            borderBottomLeftRadius={0}
            borderBottomRightRadius={0}
            onClick={() => click("right")}
          >
            R
          </Button>
          <Button
            height="60px"
            width="24px"
            minWidth="24px"
            borderTopLeftRadius={0}
            borderBottomLeftRadius={0}
            borderTopRightRadius={0}
            onClick={() => click("middle")}
          >
            M
          </Button>
        </VStack>
      </HStack>

      <VStack gap={2} width="100px">
        <Input
          value={text}
          onChange={(e) => {
            const v = e.target.value;
            if (v) typeText(v);
            setText("");
          }}
          onKeyDown={(e) => {
            // Forward control keys that aren't printable text to the host.
            if (e.key === "Backspace") {
              e.preventDefault();
              pressKey("Backspace");
            }
          }}
          placeholder="type…"
          size="md"
          autoComplete="off"
        />
        <Text fontSize="sm" color={ready ? "green.400" : "red.400"}>
          {ready ? "⚡ live" : error ? error : "connecting…"}
        </Text>
      </VStack>
    </HStack>
  );
}
