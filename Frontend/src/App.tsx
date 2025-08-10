import { Button, Center, HStack, VStack } from "@chakra-ui/react";
import "./App.css";
import { mediaControlApiClient } from "./hooks/useMediaControlApi";
import { PiPlayPauseFill } from "react-icons/pi";
import { MdSkipNext, MdSkipPrevious, MdStop } from "react-icons/md";
import { GrFormNext, GrFormPrevious } from "react-icons/gr";
import { FaVolumeDown, FaVolumeMute, FaVolumeUp } from "react-icons/fa";
import { IoIosArrowDown, IoIosArrowUp } from "react-icons/io";
import { ImYoutube2 } from "react-icons/im";

function App() {
  return (
    <>
      <Center height={"100vh"}>
        <VStack>
          <HStack>
            <Button
              paddingY={3}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeDown()}
            >
              <FaVolumeDown size={40} />
            </Button>
            <Button
              paddingY={3}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeMute()}
            >
              <FaVolumeMute size={40} />
            </Button>
            <Button
              paddingY={3}
              height={"fit-content"}
              onClick={() => mediaControlApiClient.volumeUp()}
            >
              <FaVolumeUp size={40} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.arrowUp()}
            >
              <IoIosArrowUp size={60} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.arrowLeft()}
            >
              <GrFormPrevious size={60} />
            </Button>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.play()}
            >
              <PiPlayPauseFill size={90} />
            </Button>

            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.arrowRight()}
            >
              <GrFormNext size={60} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.previous()}
            >
              <MdSkipPrevious size={40} />
            </Button>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.arrowDown()}
            >
              <IoIosArrowDown size={60} />
            </Button>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.next()}
            >
              <MdSkipNext size={40} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
              onClick={() => mediaControlApiClient.kkey()}
            >
              <ImYoutube2 size={60} />
            </Button>
          </HStack>

          <HStack>
            <Button
              height={"fit-content"}
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
