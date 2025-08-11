import { Box, Button, Center, HStack, VStack } from "@chakra-ui/react";
import "./App.css";
import { mediaControlApiClient } from "./hooks/useMediaControlApi";
import { PiPlayPauseFill } from "react-icons/pi";
import {
  MdOutlineSpaceBar,
  MdSkipNext,
  MdSkipPrevious,
  MdStop,
} from "react-icons/md";
import { GrFormNext, GrFormPrevious } from "react-icons/gr";
import { FaVolumeDown, FaVolumeMute, FaVolumeUp } from "react-icons/fa";

function App() {
  return (
    <>
      <Center height={"100vh"}>
        <VStack>
          <HStack>
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeDown()}
            >
              <FaVolumeDown size={40} />
            </Button>
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeMute()}
            >
              <FaVolumeMute size={40} />
            </Button>
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeUp()}
            >
              <FaVolumeUp size={40} />
            </Button>
          </HStack>

          <HStack height={"70px"}>
            <Button
              width={"80px"}
              height={"120px"}
              clipPath={
                "path('m71.33294,25.46534c18.54259,-24.28296 -42.52553,-39.27983 -49.80353,-6.03608c-7.278,33.24375 -7.03236,47.11036 -0.61396,75.52821c6.4184,28.41785 66.59189,28.00932 49.08427,-0.32339c-17.50761,-28.33271 -17.20938,-44.88578 1.33321,-69.16874z')"
              }
              transform={"rotate(90deg) translate(10px)"}
              onClick={() => mediaControlApiClient.arrowUp()}
            >
              <GrFormPrevious size={60} />
            </Button>
          </HStack>

          <HStack>
            <Button
              width={"80px"}
              height={"120px"}
              clipPath={
                "path('m71.33294,25.46534c18.54259,-24.28296 -42.52553,-39.27983 -49.80353,-6.03608c-7.278,33.24375 -7.03236,47.11036 -0.61396,75.52821c6.4184,28.41785 66.59189,28.00932 49.08427,-0.32339c-17.50761,-28.33271 -17.20938,-44.88578 1.33321,-69.16874z')"
              }
              onClick={() => mediaControlApiClient.arrowLeft()}
            >
              <GrFormPrevious size={60} />
            </Button>
            <Button
              width={"110px"}
              height={"100px"}
              borderRadius={"50%"}
              onClick={() => mediaControlApiClient.play()}
            >
              <PiPlayPauseFill size={70} />
            </Button>

            <Button
              width={"80px"}
              height={"120px"}
              clipPath={
                "path('m9.04602,25.46534c-18.02519,-24.28296 41.33893,-39.27983 48.41385,-6.03608c7.07492,33.24375 6.83613,47.11036 0.59683,75.52821c-6.23931,28.41785 -64.73376,28.00932 -47.71466,-0.32339c17.01909,-28.33271 16.72918,-44.88578 -1.29601,-69.16874z')"
              }
              onClick={() => mediaControlApiClient.arrowRight()}
            >
              <GrFormNext size={60} />
            </Button>
          </HStack>

          <HStack height={"70px"}>
            <Button
              height={"fit-content"}
              borderRadius={"30%"}
              onClick={() => mediaControlApiClient.previous()}
            >
              <MdSkipPrevious size={40} />
            </Button>
            <Box width={"20px"}></Box>
            <Button
              width={"80px"}
              height={"120px"}
              clipPath={
                "path('m71.33294,25.46534c18.54259,-24.28296 -42.52553,-39.27983 -49.80353,-6.03608c-7.278,33.24375 -7.03236,47.11036 -0.61396,75.52821c6.4184,28.41785 66.59189,28.00932 49.08427,-0.32339c-17.50761,-28.33271 -17.20938,-44.88578 1.33321,-69.16874z')"
              }
              transform={"rotate(-90deg) translate(10px)"}
              onClick={() => mediaControlApiClient.arrowDown()}
            >
              <GrFormPrevious size={60} />
            </Button>
            <Box width={"20px"}></Box>
            <Button
              height={"fit-content"}
              borderRadius={"30%"}
              onClick={() => mediaControlApiClient.next()}
            >
              <MdSkipNext size={40} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"50px"}
              paddingX={3}
              onClick={() => mediaControlApiClient.space()}
            >
              <MdOutlineSpaceBar size={60} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
              paddingX={0}
              onClick={() => mediaControlApiClient.stop()}
            >
              <MdStop size={60} />
            </Button>
          </HStack>
        </VStack>
      </Center>
    </>
  );
}

export default App;
