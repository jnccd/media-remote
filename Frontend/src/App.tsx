import {
  Alert,
  AlertDialog,
  AlertDialogBody,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogOverlay,
  AlertIcon,
  AlertTitle,
  Button,
  Center,
  HStack,
  IconButton,
  Input,
  InputGroup,
  InputRightElement,
  VStack,
} from "@chakra-ui/react";
import "./App.css";
import {
  postReqTo,
  setAxiosClientPassword,
} from "./services/useMediaControlApi";
import { PiPlayPauseFill } from "react-icons/pi";
import {
  MdOutlineSpaceBar,
  MdSkipNext,
  MdSkipPrevious,
  MdStop,
} from "react-icons/md";
import { GrFormNext, GrFormPrevious } from "react-icons/gr";
import { FaVolumeDown, FaVolumeMute, FaVolumeUp } from "react-icons/fa";
import { useRef, useState } from "react";
import { ViewIcon, ViewOffIcon } from "@chakra-ui/icons";
import { AxiosError } from "axios";
import { usePageHeightThreshold } from "./hooks/usePageHeightThreshold";
import { usePageWidthThreshold } from "./hooks/usePageWidthThreshold";
import InputPad from "./components/InputPad";

function App() {
  const [errorState, setErrorState] = useState(null as Error | null);
  const [password, setPassword] = useState(null as String | null);
  const [showPassword, setShowPassword] = useState(false);
  const cancelRef = useRef<HTMLButtonElement | null>(null);
  const passwordInputRef = useRef<HTMLInputElement | null>(null);

  const abovePageHeightThreshold = usePageHeightThreshold(700);
  const abovePageWidthThreshold = usePageWidthThreshold(window.innerHeight);
  const wideLayoutActive = !abovePageHeightThreshold && abovePageWidthThreshold;

  const handleMediaApiReq = async (route: string) => {
    postReqTo(
      route.replace(/[A-Z]/g, (match, offset) =>
        offset === 0 ? match.toLowerCase() : `-${match.toLowerCase()}`,
      ),
    )
      .then((res) => {
        if (res instanceof Error) {
          setErrorState(res);
        } else {
          setErrorState(null);
        }
      })
      .catch((error) => {
        setErrorState(error as Error);
      });
  };

  const passwordSubmit = (e: React.SyntheticEvent) => {
    e.preventDefault();
    setShowPassword(false);
    setAxiosClientPassword(passwordInputRef.current?.value ?? null);
    setPassword(passwordInputRef.current?.value ?? null);
  };

  return (
    <>
      {errorState && (
        <Alert
          status="error"
          position={"absolute"}
          background={"rgb(145 98 98)"}
          zIndex={1}
        >
          <AlertIcon />
          <AlertTitle>
            {"There seems to be a connection issue or the password was wrong! " +
              (errorState instanceof AxiosError
                ? `Status code: ${(errorState as AxiosError).status}`
                : "")}
          </AlertTitle>
        </Alert>
      )}

      <AlertDialog
        isOpen={!password}
        leastDestructiveRef={cancelRef}
        onClose={() => {}}
        closeOnOverlayClick={false}
        isCentered
      >
        <AlertDialogOverlay>
          <AlertDialogContent width="95%">
            <AlertDialogHeader fontSize="lg" fontWeight="bold">
              Missing Password!
            </AlertDialogHeader>

            <AlertDialogBody>
              <HStack gap={3} marginBottom={4}>
                <InputGroup size="md">
                  <Input
                    id="passwordInput"
                    ref={passwordInputRef}
                    type={showPassword ? "text" : "password"}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        passwordSubmit(e);
                      }
                    }}
                    autoFocus
                  ></Input>
                  <InputRightElement width="3rem">
                    <IconButton
                      h="1.75rem"
                      size="sm"
                      onClick={() => {
                        setShowPassword(!showPassword);
                      }}
                      icon={showPassword ? <ViewOffIcon /> : <ViewIcon />}
                      aria-label={
                        showPassword ? "Hide password" : "Show password"
                      }
                    />
                  </InputRightElement>
                </InputGroup>
                <Button
                  paddingX={6}
                  paddingY={5}
                  colorScheme="teal"
                  type="submit"
                  onClick={passwordSubmit}
                >
                  Set Password
                </Button>
              </HStack>
            </AlertDialogBody>
          </AlertDialogContent>
        </AlertDialogOverlay>
      </AlertDialog>

      <Center height={wideLayoutActive ? "80%" : "70%"}>
        <HStack paddingTop={wideLayoutActive ? "60px" : "60px"}>
          <VStack
            visibility={wideLayoutActive ? "visible" : "hidden"}
            height={wideLayoutActive ? "fit-content" : "0px"}
            padding={6}
          >
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => handleMediaApiReq("volumeDown")}
            >
              <FaVolumeDown size={40} />
            </Button>
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => handleMediaApiReq("volumeMute")}
            >
              <FaVolumeMute size={40} />
            </Button>
            <Button
              paddingY={3}
              borderRadius={"50%"}
              height={"fit-content"}
              onClick={() => handleMediaApiReq("volumeUp")}
            >
              <FaVolumeUp size={40} />
            </Button>
          </VStack>
          <VStack>
            <HStack
              visibility={!wideLayoutActive ? "visible" : "hidden"}
              height={!wideLayoutActive ? "fit-content" : "0px"}
            >
              <Button
                paddingY={3}
                borderRadius={"50%"}
                height={"fit-content"}
                onClick={() => handleMediaApiReq("volumeDown")}
              >
                <FaVolumeDown size={40} />
              </Button>
              <Button
                paddingY={3}
                borderRadius={"50%"}
                height={"fit-content"}
                onClick={() => handleMediaApiReq("volumeMute")}
              >
                <FaVolumeMute size={40} />
              </Button>
              <Button
                paddingY={3}
                borderRadius={"50%"}
                height={"fit-content"}
                onClick={() => handleMediaApiReq("volumeUp")}
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
                onClick={() => handleMediaApiReq("arrowUp")}
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
                onClick={() => handleMediaApiReq("arrowLeft")}
              >
                <GrFormPrevious size={60} />
              </Button>
              <Button
                width={"110px"}
                height={"100px"}
                borderRadius={"50%"}
                onClick={() => handleMediaApiReq("space")}
              >
                <MdOutlineSpaceBar size={60} />
              </Button>

              <Button
                width={"80px"}
                height={"120px"}
                clipPath={
                  "path('m9.04602,25.46534c-18.02519,-24.28296 41.33893,-39.27983 48.41385,-6.03608c7.07492,33.24375 6.83613,47.11036 0.59683,75.52821c-6.23931,28.41785 -64.73376,28.00932 -47.71466,-0.32339c17.01909,-28.33271 16.72918,-44.88578 -1.29601,-69.16874z')"
                }
                onClick={() => handleMediaApiReq("arrowRight")}
              >
                <GrFormNext size={60} />
              </Button>
            </HStack>

            <HStack height={"70px"}>
              <Button
                boxSize={"56px"}
                borderRadius={"full"}
                marginRight={"28px"}
                onClick={() => handleMediaApiReq("escape")}
              >
                Esc
              </Button>
              <Button
                width={"80px"}
                height={"120px"}
                clipPath={
                  "path('m71.33294,25.46534c18.54259,-24.28296 -42.52553,-39.27983 -49.80353,-6.03608c-7.278,33.24375 -7.03236,47.11036 -0.61396,75.52821c6.4184,28.41785 66.59189,28.00932 49.08427,-0.32339c-17.50761,-28.33271 -17.20938,-44.88578 1.33321,-69.16874z')"
                }
                transform={"rotate(-90deg) translate(10px)"}
                onClick={() => handleMediaApiReq("arrowDown")}
              >
                <GrFormPrevious size={60} />
              </Button>
              <Button
                boxSize={"56px"}
                borderRadius={"full"}
                marginLeft={"28px"}
                onClick={() => handleMediaApiReq("f11")}
              >
                F11
              </Button>
            </HStack>

            <HStack>
              <Button
                height={"fit-content"}
                borderRadius={"30%"}
                onClick={() => handleMediaApiReq("previous")}
              >
                <MdSkipPrevious size={40} />
              </Button>
              <Button
                height={"fit-content"}
                borderRadius={"30%"}
                onClick={() => handleMediaApiReq("play")}
              >
                <PiPlayPauseFill size={40} />
              </Button>
              <Button
                height={"fit-content"}
                borderRadius={"30%"}
                onClick={() => handleMediaApiReq("next")}
              >
                <MdSkipNext size={40} />
              </Button>
            </HStack>

            <VStack
              visibility={!wideLayoutActive ? "visible" : "hidden"}
              height={!wideLayoutActive ? "fit-content" : "0px"}
            >
              <Button
                height={"fit-content"}
                borderRadius={"30%"}
                onClick={() => handleMediaApiReq("stop")}
              >
                <MdStop size={40} />
              </Button>
            </VStack>

            <InputPad password={String(password ?? "")} />

            <Button
              margin={5}
              colorScheme="gray"
              size="lg"
              onClick={() => setPassword(null)}
            >
              Reset Password
            </Button>
          </VStack>
          <VStack
            visibility={wideLayoutActive ? "visible" : "hidden"}
            height={wideLayoutActive ? "fit-content" : "0px"}
            padding={6}
          >
            <Button
              height={"fit-content"}
              borderRadius={"30%"}
              onClick={() => handleMediaApiReq("stop")}
            >
              <MdStop size={40} />
            </Button>
          </VStack>
        </HStack>
      </Center>
    </>
  );
}

export default App;
