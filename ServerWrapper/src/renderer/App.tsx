import { MemoryRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';
import { Box, Center, HStack, Input, Text, VStack } from '@chakra-ui/react';
import { useRef, useState } from 'react';
import ConsoleView from './ConsoleView';

function Hello() {
  const [inputt, setInputt] = useState(null as string | null);
  const [showedText, setShowedText] = useState(null as string | null);

  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <Center>
      <VStack>
        {/* <HStack>
          <Text>UwU!</Text>
          <Input
            id="inputt"
            ref={inputRef}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                const inp = inputRef.current?.value ?? null;
                setInputt(inp);
                console.log(inp);
                window.api.fs.readFile(inp ?? '').then((data) => {
                  console.log(data);
                  setShowedText(data);
                });
              }
            }}
          />
          <Text>{showedText}</Text>
        </HStack> */}
        <ConsoleView />
      </VStack>
    </Center>
  );
}

export default function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Hello />} />
      </Routes>
    </Router>
  );
}
