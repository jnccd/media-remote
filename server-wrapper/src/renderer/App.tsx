import { MemoryRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';
import { Box, Center, Input, Text } from '@chakra-ui/react';
import { useRef, useState } from 'react';

function Hello() {
  const [inputt, setInputt] = useState(null as string | null);
  const [showedText, setShowedText] = useState(null as string | null);

  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <Center>
      <Box>
        <Text>UwU</Text>
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
      </Box>
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
