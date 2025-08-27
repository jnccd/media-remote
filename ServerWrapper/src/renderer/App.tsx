import { MemoryRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';
import { Box, Center, HStack, Input, Text, VStack } from '@chakra-ui/react';
import { useRef, useState } from 'react';
import ConsoleView from './ConsoleView';

function Hello() {
  return (
    <Center>
      <VStack>
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
