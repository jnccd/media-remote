import axios from "axios";
import { getAuthHeaderForMediaControlAPI } from "./useEncryption";

export const baseUrl = import.meta.env.VITE_DEV_BACKEND_ADDRESS
  ? import.meta.env.VITE_DEV_BACKEND_ADDRESS
  : window.location.href;
export const axiosClient = axios.create({
  baseURL: baseUrl,
  withCredentials: false,
});

var axiosClientPassword: string | null = null;
export const setAxiosClientPassword = (password: string | null) =>
  (axiosClientPassword = password);

export const postReqTo = (route: string) => {
  return axiosClient.post(`${route}`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: getAuthHeaderForMediaControlAPI(
        axiosClientPassword ?? "a"
      ),
    },
  });
};
